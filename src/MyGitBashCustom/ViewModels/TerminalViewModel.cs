using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Models;
using MyGitBashCustom.Services;

namespace MyGitBashCustom.ViewModels;

public sealed class TerminalViewModel : ObservableObject
{
    private readonly GitBashEngine _engine;
    private readonly UsageStatsService _stats;
    private readonly ActivityService _activity;
    private readonly SettingsService _settings;
    private readonly IntelliSenseEngine _intel;

    private string _input = "";
    private bool _busy;
    private string _cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private string _branch = "";
    private bool _suggestOpen;
    private int _suggestIndex;
    private int _histIndex = -1;

    public ObservableCollection<TerminalLine> Lines { get; } = new();
    public ObservableCollection<SuggestionItem> Suggestions { get; } = new();

    public string InputText { get => _input; set { if (Set(ref _input, value)) RefreshSuggestions(); } }
    public bool IsBusy { get => _busy; private set { if (Set(ref _busy, value)) StopCommand?.RaiseCanExecuteChanged(); } }
    public string Cwd { get => _cwd; private set => Set(ref _cwd, value); }
    public string GitBranch { get => _branch; private set => Set(ref _branch, value); }
    public bool IsSuggestOpen { get => _suggestOpen; set => Set(ref _suggestOpen, value); }
    public int SuggestIndex { get => _suggestIndex; set => Set(ref _suggestIndex, value); }
    public bool EngineReady => _engine.IsRunning;

    public AsyncRelayCommand ExecuteCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CopyLastCommand { get; }
    public RelayCommand CopyOutputCommand { get; }
    public RelayCommand AcceptSuggestionCommand { get; }
    public RelayCommand HistoryPrevCommand { get; }
    public RelayCommand HistoryNextCommand { get; }

    public event Action? InputHighlightRequested;

    public TerminalViewModel(GitBashEngine engine, UsageStatsService stats, ActivityService activity, SettingsService settings)
    {
        _engine = engine; _stats = stats; _activity = activity; _settings = settings;
        _intel = new IntelliSenseEngine(stats.HistorySnapshot, () => Cwd, stats.Snapshot);
        ExecuteCommand = new AsyncRelayCommand(ExecuteAsync, () => !IsBusy && _engine.IsRunning);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsBusy);
        ClearCommand = new RelayCommand(() => Lines.Clear());
        CopyLastCommand = new RelayCommand(() =>
        {
            try
            {
                var last = Lines.LastOrDefault(l => l.IsCommand)?.Text;
                if (string.IsNullOrWhiteSpace(last)) return;
                var prefix = _settings.Current.PromptSymbol + " ";
                Clipboard.SetText(last.StartsWith(prefix, StringComparison.Ordinal) ? last[prefix.Length..] : last);
            }
            catch { }
        });
        CopyOutputCommand = new RelayCommand(() =>
        {
            try
            {
                var all = string.Join(Environment.NewLine, Lines.Select(l => l.Text));
                if (!string.IsNullOrEmpty(all)) Clipboard.SetText(all);
            }
            catch { }
        });
        AcceptSuggestionCommand = new RelayCommand(AcceptSuggestion);
        HistoryPrevCommand = new RelayCommand(HistoryPrev);
        HistoryNextCommand = new RelayCommand(HistoryNext);
        _settings.Changed += () => OnPropertyChanged(nameof(PromptSymbol));
    }

    public void PrintWelcome(string gitVer, string bashVer)
    {
        if (!_settings.Current.ShowWelcome) return;
        Lines.Add(new TerminalLine { Text = "✨ به «ترمینال فارسی Git Bash» خوش آمدید — سریع، زیبا و هوشمند." });
        if (!string.IsNullOrEmpty(gitVer)) Lines.Add(new TerminalLine { Text = $"  {gitVer}" });
        if (!string.IsNullOrEmpty(bashVer)) Lines.Add(new TerminalLine { Text = $"  {bashVer}" });
        Lines.Add(new TerminalLine { Text = "  راهنما: بنویسید و Tab بزنید • ↑/↓ تاریخچه • دستور help برای راهنما" });
    }

    public void PrintLine(string text, bool err = false)
        => Lines.Add(new TerminalLine { Text = text, IsError = err });

    private string _title = "ترمینال";
    public string Title { get => _title; set => Set(ref _title, value); }
    public string PromptSymbol => _settings.Current.PromptSymbol;

    public void NotifyEngineReady()
    {
        OnPropertyChanged(nameof(EngineReady));
        ExecuteCommand.RaiseCanExecuteChanged();
        _ = RefreshCwdBranchAsync();
    }

    /// <summary>روشن‌کردن موتور اختصاصی این تب.</summary>
    public async Task StartAsync(string bashPath, bool welcome, string gitVer = "", string bashVer = "")
    {
        try
        {
            bool ok = await _engine.StartAsync(bashPath).ConfigureAwait(true);
            if (ok)
            {
                NotifyEngineReady();
                if (welcome) PrintWelcome(gitVer, bashVer);
                else PrintLine("▸ تب جدید آماده شد ✓");
                await RefreshCwdBranchAsync().ConfigureAwait(true);
            }
            else PrintLine("✗ روشن‌کردن موتور این تب ناموفق بود", err: true);
        }
        catch (Exception ex)
        {
            PrintLine("✗ خطا در روشن‌کردن تب: " + ex.Message, err: true);
            Services.AppLogger.Instance.Error("Terminal", "خطا در روشن‌کردن تب", ex);
        }
    }

    /// <summary>توقف دستور در حال اجرا (معادل Ctrl+C در ترمینال).</summary>
    public async Task StopAsync()
    {
        if (!IsBusy) return;
        bool ok = await _engine.InterruptAsync().ConfigureAwait(true);
        PrintLine(ok ? "⏹ درخواست توقف ارسال شد؛ در حال پایان امن فرایند…" : "⚠ توقف ممکن نشد؛ کانال کنترل Git Bash در دسترس نیست.", err: !ok);
    }

    public Task DisposeEngineAsync() => _engine.DisposeAsync().AsTask();

    private void RefreshSuggestions()
    {
        try
        {
            Suggestions.Clear();
            if (!_settings.Current.EnableIntelliSense || string.IsNullOrWhiteSpace(InputText)) { IsSuggestOpen = false; return; }
            foreach (var s in _intel.Suggest(InputText)) Suggestions.Add(s);
            IsSuggestOpen = Suggestions.Count > 0;
            SuggestIndex = 0;
        }
        catch { IsSuggestOpen = false; }
    }

    private void AcceptSuggestion()
    {
        if (!IsSuggestOpen || Suggestions.Count == 0) return;
        var i = Math.Clamp(SuggestIndex, 0, Suggestions.Count - 1);
        var s = Suggestions[i];
        // اگر پیشنهاد پرچم/مسیر است به انتها بچسبان، وگرنه جایگزین کن
        if (s.Kind is "پرچم" or "مسیر")
        {
            var parts = InputText.Split(' ');
            if (!InputText.EndsWith(' '))
            {
                parts[^1] = s.Text;
                InputText = string.Join(' ', parts) + (s.Kind == "مسیر" ? "" : " ");
            }
            else InputText += s.Text + " ";
        }
        else InputText = s.Text + (s.Kind == "دستور" || s.Kind.StartsWith("زیر") ? " " : "");
        IsSuggestOpen = false;
        InputHighlightRequested?.Invoke();
    }

    private IReadOnlyList<string> Hist => _stats.HistorySnapshot();
    private void HistoryPrev()
    {
        var h = Hist; if (h.Count == 0) return;
        _histIndex = _histIndex < 0 ? h.Count - 1 : Math.Max(0, _histIndex - 1);
        InputText = h[_histIndex];
    }
    private void HistoryNext()
    {
        var h = Hist; if (h.Count == 0) return;
        if (_histIndex < 0) return;
        _histIndex++;
        if (_histIndex >= h.Count) { _histIndex = -1; InputText = ""; }
        else InputText = h[_histIndex];
    }

    public async Task ExecuteAsync()
    {
        var cmd = (InputText ?? "").Trim();
        if (string.IsNullOrEmpty(cmd)) return;
        if (cmd.Equals("clear", StringComparison.OrdinalIgnoreCase) || cmd.Equals("cls", StringComparison.OrdinalIgnoreCase))
        { Lines.Clear(); InputText = ""; _histIndex = -1; return; }
        if (cmd.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            Lines.Add(new TerminalLine { Text = $"{_settings.Current.PromptSymbol} {cmd}", IsCommand = true });
            Lines.Add(new TerminalLine { Text = "دستورات داخلی: clear (پاک‌کردن) • help (این راهنما) • بقیه مستقیم به Bash ارسال می‌شود." });
            InputText = ""; return;
        }
        IsBusy = true; ExecuteCommand.RaiseCanExecuteChanged();
        Lines.Add(new TerminalLine { Text = $"{_settings.Current.PromptSymbol} {cmd}", IsCommand = true });
        InputText = ""; _histIndex = -1; IsSuggestOpen = false;
        var sw = Stopwatch.StartNew();
        try
        {
            var r = await _engine.ExecuteAsync(cmd, TimeSpan.FromMinutes(5)).ConfigureAwait(true);
            sw.Stop();
            AppendOutput(r.Output, false);
            if (!string.IsNullOrWhiteSpace(r.Error)) AppendOutput(r.Error, true);
            bool ok = r.ExitCode == 0;
            Lines.Add(new TerminalLine
            {
                Text = ok ? $"✓ تمام شد در {PersianHelper.FormatDuration(r.DurationMs)}" : $"✗ کد خروج {r.ExitCode}",
                IsError = !ok, IsSuccess = ok
            });
            while (Lines.Count > 2000) Lines.RemoveAt(0);
            _stats.Record(cmd, ok, r.DurationMs);
            await _activity.AppendAsync(new ActivityEntry
            {
                Timestamp = DateTime.Now, Command = cmd.Length > 500 ? cmd[..500] : cmd,
                BaseCommand = UsageStatsService.BaseOf(cmd), ExitCode = r.ExitCode,
                DurationMs = r.DurationMs, WorkDir = Cwd
            }).ConfigureAwait(true);
            AppLogger.Instance.Debug("Terminal", $"اجرای «{cmd}» → کد {r.ExitCode} در {r.DurationMs}ms");
            await RefreshCwdBranchAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Lines.Add(new TerminalLine { Text = "خطای اجرای دستور: " + ex.Message, IsError = true });
            AppLogger.Instance.Error("Terminal", "خطای اجرای دستور", ex);
        }
        finally { IsBusy = false; ExecuteCommand.RaiseCanExecuteChanged(); }
    }

    private void AppendOutput(string text, bool err)
    {
        if (string.IsNullOrEmpty(text)) return;
        foreach (var line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var t = line.TrimEnd();
            if (string.IsNullOrEmpty(t)) continue;
            // حذف escape های ANSI
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\x1B\[[0-9;?]*[a-zA-Z]", "");
            if (t.Contains("READY")) continue;
            Lines.Add(new TerminalLine { Text = t.Length > 2000 ? t[..2000] + "…" : t, IsError = err });
        }
    }

    public async Task RefreshCwdBranchAsync()
    {
        try
        {
            await _engine.RefreshWorkDirAsync().ConfigureAwait(true);
            var wd = _engine.WorkDir;
            if (Directory.Exists(wd)) Cwd = wd;
            if (_engine.IsRunning)
            {
                var b = await _engine.ExecuteAsync("git branch --show-current 2>/dev/null", TimeSpan.FromSeconds(5)).ConfigureAwait(true);
                GitBranch = b.Output.Trim().Split('\n').LastOrDefault()?.Trim() ?? "";
            }
        }
        catch { }
    }

    public void SendToInput(string text) => InputText = text + " ";
}
