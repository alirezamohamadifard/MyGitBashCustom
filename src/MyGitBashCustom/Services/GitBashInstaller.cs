using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace MyGitBashCustom.Services;

/// <summary>
/// دانلود و نصب Git for Windows فقط با اجازه صریح کاربر.
/// </summary>
public sealed class GitBashInstaller
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };
    static GitBashInstaller()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("MyGitBashCustom/1.0");
    }

    public async Task<string> ResolveLatestUrlAsync(CancellationToken ct = default)
    {
        // پیش‌فرض پایدار (اگر API در دسترس نبود)
        const string fallback = "https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/Git-2.47.1-64-bit.exe";
        try
        {
            using var r = await Http.GetAsync("https://api.github.com/repos/git-for-windows/git/releases/latest", ct).ConfigureAwait(false);
            r.EnsureSuccessStatusCode();
            using var s = await r.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(s, cancellationToken: ct).ConfigureAwait(false);
            foreach (var a in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("Git-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith("64-bit.exe", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("portable", StringComparison.OrdinalIgnoreCase))
                    return a.GetProperty("browser_download_url").GetString() ?? fallback;
            }
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Installer", "دریافت آخرین نسخه از گیت‌هاب ناموفق بود؛ از نسخه پایدار استفاده می‌شود", ex); }
        return fallback;
    }

    public async Task<string> DownloadAsync(string url, IProgress<double> progress, CancellationToken ct = default)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "Git-Setup-" + Guid.NewGuid().ToString("N") + ".exe");
        AppLogger.Instance.Info("Installer", $"شروع دانلود: {url}");
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;
        await using var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dst = File.Create(tmp);
        var buf = new byte[81920];
        long read = 0; int n;
        while ((n = await src.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total > 0) progress.Report(100.0 * read / total);
        }
        AppLogger.Instance.Info("Installer", $"دانلود کامل شد: {tmp}");
        return tmp;
    }

    /// <summary>اجرای نصب با UAC؛ حتماً قبلش از کاربر اجازه گرفته شود.</summary>
    public void RunInstall(string setupPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = setupPath,
            Arguments = "/VERYSILENT /NORESTART /NOCANCEL /SP- /CLOSEAPPLICATIONS",
            UseShellExecute = true,
            Verb = "runas"
        };
        AppLogger.Instance.Info("Installer", "اجرای نصب Git for Windows با دسترسی ادمین");
        Process.Start(psi);
    }
}
