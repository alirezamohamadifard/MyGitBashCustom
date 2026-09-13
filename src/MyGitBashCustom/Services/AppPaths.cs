using System.IO;

namespace MyGitBashCustom.Services;

/// <summary>
/// مسیر داده‌ها (تنظیمات، آمار، تاریخچه، لاگ‌ها، فعالیت).
/// پیش‌فرض: پوشه data کنار فایل اجرایی (حالت portable) و در صورت عدم دسترسی، AppData.
/// </summary>
public static class AppPaths
{
    private static string _root = ResolveAuto();

    public static string Root => _root;
    public static string BaseDir => AppContext.BaseDirectory;
    public static string PortableRoot => Path.Combine(BaseDir, "data");
    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyGitBashCustom");

    public static bool IsPortable => string.Equals(
        Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar),
        Path.GetFullPath(PortableRoot).TrimEnd(Path.DirectorySeparatorChar),
        StringComparison.OrdinalIgnoreCase);

    public static string SettingsFile => Path.Combine(_root, "settings.json");
    public static string StatsFile => Path.Combine(_root, "stats.json");
    public static string HistoryFile => Path.Combine(_root, "history.txt");
    public static string LogsDir => Path.Combine(_root, "logs");
    public static string ActivityDir => Path.Combine(_root, "activity");

    static AppPaths() => Ensure();

    public static string ResolveAuto()
        => IsWritable(PortableRoot) ? PortableRoot : AppDataRoot;

    public static void Configure(string root)
    {
        _root = string.IsNullOrWhiteSpace(root) ? ResolveAuto() : root;
        Ensure();
    }

    public static void Ensure()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(ActivityDir);
    }

    public static bool IsWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, ".writetest");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    /// <summary>انتقال داده‌ها از ریشه قبلی (فقط فایل‌های قدیمی‌تر مقصد رونویسی می‌شوند).</summary>
    public static void Migrate(string fromRoot, string toRoot)
    {
        try
        {
            if (string.Equals(Path.GetFullPath(fromRoot), Path.GetFullPath(toRoot), StringComparison.OrdinalIgnoreCase))
                return;
            Directory.CreateDirectory(toRoot);
            foreach (var f in new[] { "settings.json", "stats.json", "history.txt" })
                CopyIfNewer(Path.Combine(fromRoot, f), Path.Combine(toRoot, f));
            foreach (var sub in new[] { "logs", "activity" })
            {
                var src = Path.Combine(fromRoot, sub);
                if (!Directory.Exists(src)) continue;
                var dst = Path.Combine(toRoot, sub);
                Directory.CreateDirectory(dst);
                foreach (var f in Directory.GetFiles(src))
                    CopyIfNewer(f, Path.Combine(dst, Path.GetFileName(f)));
            }
            AppLogger.Instance.Info("Data", $"داده‌ها به محل جدید منتقل شد: {toRoot}");
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Data", "انتقال داده‌ها ناموفق بود", ex); }
    }

    private static void CopyIfNewer(string src, string dst)
    {
        try
        {
            if (!File.Exists(src)) return;
            if (!File.Exists(dst) || File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst))
                File.Copy(src, dst, true);
        }
        catch { }
    }
}
