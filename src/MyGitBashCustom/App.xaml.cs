using System.Windows;
using System.Windows.Media;
using MyGitBashCustom.Models;
using MyGitBashCustom.Services;

namespace MyGitBashCustom;

public partial class App : Application
{
    public static SettingsService Settings { get; } = new();
    public static UsageStatsService Stats { get; } = new();
    public static ActivityService Activity { get; } = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        AppLogger.Instance.AttachDispatcher(Dispatcher);
        AppDomain.CurrentDomain.UnhandledException += (_, a) =>
            AppLogger.Instance.Fatal("App", "خطای مهارنشده دامنه", a.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, a) =>
        {
            AppLogger.Instance.Error("UI", "خطای مهارنشده رابط کاربری", a.Exception);
            a.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, a) =>
            AppLogger.Instance.Error("Task", "خطای تسک مشاهده‌نشده", a.Exception);

        await Settings.LoadAsync().ConfigureAwait(true);
        ApplyDataLocation();
        await Settings.LoadAsync().ConfigureAwait(true); // خواندن مجدد از محل نهایی
        await Stats.LoadAsync().ConfigureAwait(true);
        ApplyTheme();
        AppLogger.Instance.Info("App", $"برنامه شروع شد v1.2.0 — داده‌ها: {AppPaths.Root}");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            await Stats.SaveNowAsync().ConfigureAwait(false);
            await Settings.SaveNowAsync().ConfigureAwait(false);
        }
        catch { }
        AppLogger.Instance.Info("App", "برنامه بسته شد");
        base.OnExit(e);
    }

    /// <summary>اعمال محل ذخیره داده طبق تنظیمات (پیش‌فرض: کنار برنامه).</summary>
    private static void ApplyDataLocation()
    {
        try
        {
            var s = Settings.Current;
            string target = s.DataLocation switch
            {
                DataLocation.Portable => AppPaths.PortableRoot,
                DataLocation.AppData => AppPaths.AppDataRoot,
                DataLocation.Custom when !string.IsNullOrWhiteSpace(s.CustomDataPath) => s.CustomDataPath!,
                _ => AppPaths.ResolveAuto(),
            };
            if (!string.Equals(
                    System.IO.Path.GetFullPath(target),
                    System.IO.Path.GetFullPath(AppPaths.Root),
                    StringComparison.OrdinalIgnoreCase))
            {
                AppPaths.Migrate(AppPaths.Root, target);
                AppPaths.Configure(target);
            }
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Data", "اعمال محل داده ناموفق بود", ex); }
    }

    public void ApplyTheme()
    {
        try
        {
            var s = Settings.Current;
            Color bg, panel, panel2, fg, muted, border;
            switch (s.Theme)
            {
                case AppTheme.Light:
                    bg = c("#F7F9FC"); panel = c("#FFFFFF"); panel2 = c("#EDF2F7"); fg = c("#172033"); muted = c("#617086"); border = c("#D7E0EA"); break;
                case AppTheme.Midnight:
                    bg = c("#070A12"); panel = c("#0D1320"); panel2 = c("#151F33"); fg = c("#E7EDFF"); muted = c("#8997B8"); border = c("#253554"); break;
                case AppTheme.TerminalGreen:
                    bg = c("#041109"); panel = c("#071B0E"); panel2 = c("#0D2A16"); fg = c("#D7F7E0"); muted = c("#82B892"); border = c("#174526"); break;
                default:
                    bg = c("#0B0F16"); panel = c("#121924"); panel2 = c("#1A2432"); fg = c("#EAF1F8"); muted = c("#91A0B4"); border = c("#2A384A"); break;
            }
            Color accent = s.Accent switch
            {
                AccentColor.Green => c("#3FB950"),
                AccentColor.Purple => c("#A371F7"),
                AccentColor.Orange => c("#D29922"),
                AccentColor.Pink => c("#F778BA"),
                _ => c("#32B5FF"),
            };
            Set("Bg", bg); Set("Panel", panel); Set("Panel2", panel2);
            Set("Fg", fg); Set("Muted", muted); Set("Border", border); Set("Accent", accent);
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Theme", "اعمال تم ناموفق بود", ex); }
    }

    private static Color c(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private void Set(string key, Color color)
    {
        if (Resources[key] is SolidColorBrush b)
        {
            if (b.IsFrozen) Resources[key] = new SolidColorBrush(color);
            else b.Color = color;
        }
        else Resources[key] = new SolidColorBrush(color);
    }
}
