using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MyGitBashCustom.Services;

public sealed record BashDetection(bool Found, string? BashPath, string? GitVersion, string? BashVersion, string Source);

/// <summary>تشخیص هسته Git Bash ویندوز از رجیستری، مسیرهای استاندارد و PATH.</summary>
public sealed class GitBashLocator
{
    private static readonly string[] Probes =
    [
        @"C:\Program Files\Git\bin\bash.exe",
        @"C:\Program Files (x86)\Git\bin\bash.exe",
        @"C:\Git\bin\bash.exe",
    ];

    public async Task<BashDetection> DetectAsync(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            var v = await GetVersionsAsync(customPath).ConfigureAwait(false);
            return new BashDetection(true, customPath, v.git, v.bash, "مسیر دستی کاربر");
        }
        foreach (var p in Probes)
            if (File.Exists(p))
            {
                var v = await GetVersionsAsync(p).ConfigureAwait(false);
                return new BashDetection(true, p, v.git, v.bash, "مسیر استاندارد");
            }
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\GitForWindows");
            var dir = k?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(dir))
            {
                var bash = Path.Combine(dir, @"bin\bash.exe");
                if (File.Exists(bash))
                {
                    var v = await GetVersionsAsync(bash).ConfigureAwait(false);
                    return new BashDetection(true, bash, v.git, v.bash, "رجیستری");
                }
            }
        }
        catch (Exception ex) { AppLogger.Instance.Warn("GitBash", "خواندن رجیستری ناموفق بود", ex); }

        var bashOnPath = FindOnPath("bash.exe");
        var gitOnPath = FindOnPath("git.exe");
        string? fromPath = null;
        if (bashOnPath is not null)
            fromPath = bashOnPath;
        else if (gitOnPath is not null && Path.GetDirectoryName(gitOnPath) is string gd)
            fromPath = Path.Combine(gd, "..", "bin", "bash.exe");
        if (fromPath is not null)
        {
            try
            {
                var full = Path.GetFullPath(fromPath);
                if (File.Exists(full))
                {
                    var v = await GetVersionsAsync(full).ConfigureAwait(false);
                    return new BashDetection(true, full, v.git, v.bash, "متغیر PATH");
                }
            }
            catch { }
        }
        return new BashDetection(false, null, null, null, "یافت نشد");
    }

    private static string? FindOnPath(string exe)
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            foreach (var d in paths)
            {
                try
                {
                    var c = Path.Combine(d.Trim().Trim('"'), exe);
                    if (File.Exists(c)) return c;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    private static async Task<(string? git, string? bash)> GetVersionsAsync(string bashPath)
    {
        string? bash = null, git = null;
        try
        {
            bash = await RunCapAsync(bashPath, "--version").ConfigureAwait(false);
            var gitExe = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(bashPath)!)!, @"cmd\git.exe");
            if (!File.Exists(gitExe))
                gitExe = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(bashPath)!)!, @"bin\git.exe");
            if (File.Exists(gitExe)) git = await RunCapAsync(gitExe, "--version").ConfigureAwait(false);
        }
        catch (Exception ex) { AppLogger.Instance.Warn("GitBash", "دریافت نسخه ناموفق بود", ex); }
        return (git?.Trim(), bash?.Trim());
    }

    private static async Task<string?> RunCapAsync(string exe, string args)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = exe, Arguments = args, UseShellExecute = false,
                RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            p.Start();
            var task = p.StandardOutput.ReadToEndAsync();
            var err = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync().ConfigureAwait(false);
            return (await task.ConfigureAwait(false) + await err.ConfigureAwait(false)).Trim();
        }
        catch { return null; }
    }
}
