using System.Diagnostics;
using System.Text;

namespace MyGitBashCustom.Services;

public sealed record BashResult(string Output, string Error, int ExitCode, long DurationMs);

/// <summary>
/// موتور اجرای Bash: یک پروسه ماندگار bash --login -i با stdin/stdout ریدایرکت‌شده.
/// خروجی هر دستور با مارکر یکتا جدا می‌شود؛ کاملاً async و thread-safe.
/// هر دستور غیرحالت‌دار در یک Job پس‌زمینه با PID ثبت‌شده اجرا می‌شود تا با
/// کانال کنترل دوم بتوان آن را واقعاً متوقف کرد (Kill گروه فرایندی).
/// </summary>
public sealed class GitBashEngine : IAsyncDisposable
{
    private Process? _proc;
    private GitBashEngine? _control;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StringBuilder _pending = new();
    private TaskCompletionSource<BashResult>? _tcs;
    private string _marker = "";
    private long _startMs;
    private string _workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private bool _jobControl;
    private readonly string _pidFile = "/tmp/mgb_" + Guid.NewGuid().ToString("N") + ".pid";

    /// <summary>دستورهای حالت‌دار که باید در خود شل اصلی اجرا شوند (نه Job جدا).</summary>
    private static readonly HashSet<string> InlineBuiltins = new(StringComparer.OrdinalIgnoreCase)
    {
        "cd", "pushd", "popd", "dirs", "export", "readonly", "unset",
        "alias", "unalias", "set", "shopt", "trap", "source", ".",
        "exec", "exit", "logout", "umask", "ulimit", "hash",
        "declare", "local", "let", "history", "enable"
    };

    public string? BashPath { get; private set; }
    public bool IsRunning => _proc is { HasExited: false };
    public string WorkDir => _workDir;
    /// <summary>برای کانال کنترل دوم: خودش کانال دیگری نمی‌سازد.</summary>
    public bool IsControlChannel { get; set; }
    public bool JobControl => _jobControl;
    public event Action<string>? RawOutput;

    public async Task<bool> StartAsync(string bashPath, CancellationToken ct = default)
    {
        await StopAsync().ConfigureAwait(false);
        BashPath = bashPath;
        try
        {
            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = bashPath,
                    Arguments = "--login -i",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = _workDir
                },
                EnableRaisingEvents = true
            };
            _proc.OutputDataReceived += OnOut;
            _proc.ErrorDataReceived += OnOut;
            _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            // خاموش‌کردن prompt اضافه و فعال‌سازی UTF-8 و حالت مانیتور (Job جدا)
            await Task.Delay(400, ct).ConfigureAwait(false);
            await ExecuteInternalAsync("export PS1=''; stty -echo 2>/dev/null; set -m 2>/dev/null; echo READY", TimeSpan.FromSeconds(8), ct).ConfigureAwait(false);
            if (!IsControlChannel)
            {
                _jobControl = await DetectJobControlAsync().ConfigureAwait(false);
                _control = new GitBashEngine { IsControlChannel = true };
                if (!await _control.StartAsync(bashPath, ct).ConfigureAwait(false))
                {
                    AppLogger.Instance.Warn("Bash", "کانال کنترل روشن نشد؛ توقف دستور (Ctrl+C) در دسترس نیست");
                    _control = null;
                }
            }
            AppLogger.Instance.Info("Bash", $"موتور Bash روشن شد: {bashPath} (JobControl={_jobControl})");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error("Bash", "روشن‌کردن موتور Bash ناموفق بود", ex);
            return false;
        }
    }

    private async Task<bool> DetectJobControlAsync()
    {
        try
        {
            // تست عملکردی مستقیم: آیا می‌توان به گروه فرایندی یک Job سیگنال داد؟
            var r = await ExecuteInternalAsync(
                "set -m 2>/dev/null; sleep 0.3 & JP=$!; if kill -0 -$JP 2>/dev/null; then echo JOBCTL:YES; else echo JOBCTL:NO; fi; wait $JP 2>/dev/null",
                TimeSpan.FromSeconds(8), CancellationToken.None).ConfigureAwait(false);
            bool ok = r.Output.Contains("JOBCTL:YES");
            AppLogger.Instance.Debug("Bash", $"تشخیص JobControl: {(ok ? "YES" : "NO")}");
            return ok;
        }
        catch { return false; }
    }

    private void OnOut(object sender, DataReceivedEventArgs e)
    {
        if (e.Data is null) return;
        RawOutput?.Invoke(e.Data);
        lock (_pending)
        {
            if (_tcs is null) return;
            var line = e.Data;
            var idx = line.IndexOf(_marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                // فرمت دقیق خط مارکر واقعی: MARKER:digits و نه چیز دیگر.
                // (خط اکوی ورودی مثل echo MARKER:$? دنباله عددی ندارد و نادیده گرفته می‌شود)
                var tail = line[(idx + _marker.Length)..];
                var digits = tail.StartsWith(':')
                    ? new string(tail[1..].TakeWhile(char.IsDigit).ToArray())
                    : "";
                if (digits.Length > 0 && digits.Length == tail.Trim().TrimStart(':').Length
                    && int.TryParse(digits, out var c))
                {
                    _pending.AppendLine(line[..idx]);
                    var tcs = _tcs; _tcs = null;
                    var dur = Environment.TickCount64 - _startMs;
                    tcs.TrySetResult(new BashResult(_pending.ToString(), "", c, dur));
                    _pending.Clear();
                }
                else _pending.AppendLine(line); // اکو یا متن عادی حاوی مارکر
            }
            else _pending.AppendLine(line);
        }
    }

    public Task<BashResult> ExecuteAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
        => ExecuteInternalAsync(command, timeout ?? TimeSpan.FromSeconds(60), ct);

    private async Task<BashResult> ExecuteInternalAsync(string command, TimeSpan timeout, CancellationToken ct)
    {
        if (!IsRunning || _proc is null)
            return new BashResult("", "موتور Bash روشن نیست. ابتدا وضعیت Git Bash را بررسی کنید.", 1, 0);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string marker, line1, line2;
            lock (_pending)
            {
                _pending.Clear();
                marker = "__MGB_" + Guid.NewGuid().ToString("N");
                _marker = marker; _startMs = Environment.TickCount64;
                _tcs = new TaskCompletionSource<BashResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            if (IsInlineCommand(command))
            {
                // دستور حالت‌دار: مستقیم در شل اصلی (سریع، بدون Job)
                line1 = $" {command}";
                line2 = $"echo {marker}:$?";
                await _proc.StandardInput.WriteLineAsync(line1).ConfigureAwait(false);
                await _proc.StandardInput.WriteLineAsync(line2).ConfigureAwait(false);
            }
            else
            {
                // دستور عادی: در Job پس‌زمینه با PID ثبت‌شده تا قابل توقف (Ctrl+C) باشد
                line1 = $"{{ trap - INT; {command}; }} & MGB_PID=$!; echo -n $MGB_PID > \"{_pidFile}\"; wait $MGB_PID; echo {marker}:$?";
                line2 = "";
                await _proc.StandardInput.WriteLineAsync(line1).ConfigureAwait(false);
            }
            await _proc.StandardInput.FlushAsync().ConfigureAwait(false);
            TaskCompletionSource<BashResult> tcs;
            lock (_pending) { tcs = _tcs ?? new TaskCompletionSource<BashResult>(TaskCreationOptions.RunContinuationsAsynchronously); }
            var delay = Task.Delay(timeout, ct);
            var completed = await Task.WhenAny(tcs.Task, delay).ConfigureAwait(false);
            if (completed != tcs.Task)
            {
                // پیش از آزادکردن gate، Job قبلی را واقعاً متوقف می‌کنیم؛ وگرنه خروجی آن
                // می‌تواند نتیجه دستور بعدی را آلوده کند.
                if (!IsControlChannel) await InterruptAsync().ConfigureAwait(false);
                TaskCompletionSource<BashResult>? pending;
                string partial;
                lock (_pending) { partial = _pending.ToString(); pending = _tcs; _tcs = null; }
                pending?.TrySetResult(new BashResult(partial, ct.IsCancellationRequested ? "اجرای دستور لغو شد." : "زمان اجرای دستور تمام شد (Timeout).", ct.IsCancellationRequested ? 130 : 124, Environment.TickCount64 - _startMs));
            }
            var result = await tcs.Task.ConfigureAwait(false);
            return result with { Output = FilterEcho(result.Output, line1, line2) };
        }
        finally { _gate.Release(); }
    }

    /// <summary>حذف اکوی ورودی و پیام‌های Job (‏[1] 1587‏ و Done) از خروجی.</summary>
    private static string FilterEcho(string output, string sent1, string sent2)
    {
        try
        {
            var a = sent1.Trim();
            var b = (sent2 ?? "").Trim();
            var kept = new List<string>();
            bool started = false;
            foreach (var l in output.Replace("\r\n", "\n").Split('\n'))
            {
                var t = l.Trim();
                // اکوی ورودی ممکن است وسط خروجی هم باشد (خط دوم بعد از اتمام خط اول خوانده می‌شود)
                if (t == a || (b.Length > 0 && t == b)) continue;
                // پیام‌های Job شل: [1] 1587 و [1]+ Done ...
                if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^\[\d+\][+-]?\s+(\d+\s*)?$")
                    || System.Text.RegularExpressions.Regex.IsMatch(t, @"^\[\d+\][+-]\s+(Done|Exit|Running|Terminated|Killed|Interrupt|Suspended|Stopped)\b"))
                    continue;
                if (!started && t.Length == 0) continue;
                started = true;
                kept.Add(l);
            }
            return string.Join("\n", kept).TrimEnd();
        }
        catch { return output; }
    }

    private static bool IsInlineCommand(string command)
    {
        var t = command.TrimStart();
        if (t.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase)) return false;
        var tokens = t.Split([' ', '\t', ';', '|', '&', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return true;
        int i = 0;
        if (tokens[0] is "command" or "builtin" && tokens.Length > 1) i = 1;
        var first = tokens[i];
        // انتساب متغیر مثل FOO=bar حالت‌دار است
        if (System.Text.RegularExpressions.Regex.IsMatch(first, @"^[A-Za-z_][A-Za-z0-9_]*\+?=")) return true;
        return InlineBuiltins.Contains(first);
    }

    public async Task RefreshWorkDirAsync()
    {
        try
        {
            var r = await ExecuteAsync("pwd -W 2>/dev/null || pwd", TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            var p = r.Output.Trim().Split('\n').LastOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(p))
            {
                // تبدیل مسیر cygwin به ویندوزی در صورت نیاز
                if (p.StartsWith("/c/", StringComparison.OrdinalIgnoreCase) || p.StartsWith("/d/", StringComparison.OrdinalIgnoreCase))
                {
                    var drive = char.ToUpper(p[1]);
                    _workDir = $"{drive}:" + p[2..].Replace('/', '\\');
                }
                else if (p.Length > 2 && p[1] == ':') _workDir = p;
            }
        }
        catch { }
    }

    /// <summary>
    /// توقف واقعی دستور در حال اجرا (معادل Ctrl+C): Kill گروه فرایندی Job
    /// از طریق کانال کنترل دوم. چون stdin پایپ است، بایت ETX به‌تنهایی اثر ندارد.
    /// </summary>
    public async Task<bool> InterruptAsync()
    {
        try
        {
            string? marker;
            TaskCompletionSource<BashResult>? tcs;
            lock (_pending) { marker = _marker; tcs = _tcs; }
            if (tcs is null || string.IsNullOrEmpty(marker)) return false;
            if (_control is not { IsRunning: true })
            {
                AppLogger.Instance.Warn("Bash", "کانال کنترل فعال نیست؛ توقف ممکن نیست");
                return false;
            }
            // ابتدا SIGINT برای shutdown تمیز؛ سپس TERM و فقط در آخر KILL.
            // سرورهای ASP.NET/Node فرصت اجرای shutdown hook و آزادکردن پورت را دارند.
            var killCmd = _jobControl
                ? $"P=$(cat \"{_pidFile}\" 2>/dev/null); if [ -n \"$P\" ]; then kill -INT -$P 2>/dev/null; for i in 1 2 3 4 5 6 7 8; do kill -0 -$P 2>/dev/null || break; sleep 0.5; done; if kill -0 -$P 2>/dev/null; then kill -TERM -$P 2>/dev/null; sleep 1; fi; kill -0 -$P 2>/dev/null && kill -KILL -$P 2>/dev/null; fi; rm -f \"{_pidFile}\"; echo intr-done"
                : $"P=$(cat \"{_pidFile}\" 2>/dev/null); if [ -n \"$P\" ]; then kill -INT $P 2>/dev/null; for i in 1 2 3 4 5 6 7 8; do kill -0 $P 2>/dev/null || break; sleep 0.5; done; if kill -0 $P 2>/dev/null; then kill -TERM $P 2>/dev/null; sleep 1; fi; kill -0 $P 2>/dev/null && kill -KILL $P 2>/dev/null; fi; rm -f \"{_pidFile}\"; echo intr-done";
            await _control.ExecuteAsync(killCmd, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            AppLogger.Instance.Debug("Bash", "سیگنال توقف (Ctrl+C) ارسال شد");
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn("Bash", "ارسال سیگنال توقف ناموفق بود", ex);
            return false;
        }
    }

    public async Task StopAsync()
    {
        try
        {
            if (_control is not null)
            {
                try { await _control.StopAsync().ConfigureAwait(false); } catch { }
                _control = null;
            }
            if (_proc is { HasExited: false })
            {
                try { await _proc.StandardInput.WriteLineAsync("exit").ConfigureAwait(false); } catch { }
                if (!_proc.WaitForExit(800)) _proc.Kill(true);
            }
            _proc?.Dispose(); _proc = null;
        }
        catch { }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
