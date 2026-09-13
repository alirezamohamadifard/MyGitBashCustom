using System.Windows;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Services;

namespace MyGitBashCustom.ViewModels;

public sealed class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly GitBashLocator _locator = new();
    private readonly GitBashInstaller _installer = new();
    private readonly SettingsService _settings;
    private readonly UsageStatsService _stats;
    private readonly ActivityService _activity;

    private int _tab;
    private string _status = "در حال بررسی Git Bash…";
    private string _versions = "";
    private bool _hasBash;
    private bool _installing;
    private double _progress;

    public TerminalManagerViewModel Terminals { get; }
    public CommandsViewModel Commands { get; } = new();
    public StatsViewModel Stats { get; }
    public ActivityViewModel Activity { get; }
    public LogsViewModel Logs { get; } = new();
    public SettingsViewModel SettingsVm { get; }

    public int SelectedTab { get => _tab; set => Set(ref _tab, value); }
    public string BashStatus { get => _status; set => Set(ref _status, value); }
    public string Versions { get => _versions; set => Set(ref _versions, value); }
    public bool HasBash { get => _hasBash; set { if (Set(ref _hasBash, value)) { InstallCommand.RaiseCanExecuteChanged(); StartCommand.RaiseCanExecuteChanged(); } } }
    public bool Installing { get => _installing; set => Set(ref _installing, value); }
    public double InstallProgress { get => _progress; set => Set(ref _progress, value); }

    public AsyncRelayCommand CheckCommand { get; }
    public AsyncRelayCommand InstallCommand { get; }
    public AsyncRelayCommand StartCommand { get; }
    public RelayCommand GoTabCommand { get; }

    public event Action<TerminalViewModel>? RenameTabRequested;

    public MainViewModel(SettingsService settings, UsageStatsService stats, ActivityService activity)
    {
        _settings = settings; _stats = stats; _activity = activity;
        Terminals = new TerminalManagerViewModel(() => new GitBashEngine(), stats, activity, settings);
        Terminals.RenameRequested += t => RenameTabRequested?.Invoke(t);
        Stats = new StatsViewModel(stats);
        Activity = new ActivityViewModel(activity);
        SettingsVm = new SettingsViewModel(settings, () => Terminals.AnyRunning);
        Commands.SendToTerminal += t => { if (Terminals.Selected is not null) Terminals.Selected.SendToInput(t); SelectedTab = 0; };
        CheckCommand = new AsyncRelayCommand(() => CheckAsync(false));
        InstallCommand = new AsyncRelayCommand(InstallFlowAsync, () => !HasBash && !Installing);
        StartCommand = new AsyncRelayCommand(() => CheckAsync(true), () => !Installing);
        GoTabCommand = new RelayCommand(p => { if (p is string s && int.TryParse(s, out var i)) SelectedTab = i; });
        SettingsVm.Applied += ApplyVisualSettings;
        ApplyVisualSettings();
    }

    private void ApplyVisualSettings()
    {
        if (Application.Current is not App app) return;
        app.ApplyTheme();
        if (app.MainWindow is Window window)
        {
            var font = _settings.Current.FontFamily;
            if (!string.IsNullOrWhiteSpace(font)) window.FontFamily = new System.Windows.Media.FontFamily(font);
            window.FlowDirection = _settings.Current.RtlUi ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }

    public async Task CheckAsync(bool startEngine)
    {
        try
        {
            BashStatus = "در حال بررسی Git Bash…";
            var det = await _locator.DetectAsync(_settings.Current.CustomBashPath).ConfigureAwait(true);
            if (det.Found)
            {
                HasBash = true;
                Versions = $"{det.GitVersion} • {det.BashVersion}";
                BashStatus = $"✓ متصل: {det.BashPath} ({det.Source})";
                AppLogger.Instance.Info("GitBash", $"یافت شد: {det.BashPath} | {det.GitVersion} | {det.BashVersion}");
                if (startEngine || !Terminals.AnyRunning)
                {
                    await Terminals.EnsureStartedAsync(det.BashPath!, det.GitVersion ?? "", det.BashVersion ?? "").ConfigureAwait(true);
                    BashStatus = Terminals.AnyRunning
                        ? $"✓ موتور روشن — {det.Source} ({Terminals.Tabs.Count} تب)"
                        : "✗ روشن‌کردن موتور ناموفق بود — لاگ‌ها را ببینید";
                }
            }
            else
            {
                HasBash = false; Versions = "";
                BashStatus = "✗ Git Bash یافت نشد";
                AppLogger.Instance.Warn("GitBash", "Git Bash روی سیستم یافت نشد");
                if (_settings.Current.AutoCheckGitBash)
                    await PromptInstallAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            BashStatus = "خطا در بررسی: " + ex.Message;
            AppLogger.Instance.Error("GitBash", "خطا در بررسی Git Bash", ex);
        }
    }

    private async Task PromptInstallAsync()
    {
        if (Installing) return;
        var ask = !_settings.Current.ConfirmBeforeInstall || MessageBox.Show(
            "هسته Git Bash روی سیستم شما نصب نیست.\n\nآیا مایلید آخرین نسخه رسمی Git for Windows دانلود و نصب شود؟\n(دانلود از github.com — نصب با اجازه ادمین)",
            "Git Bash یافت نشد", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        if (ask) await InstallFlowAsync().ConfigureAwait(true);
        else AppLogger.Instance.Info("Installer", "کاربر نصب خودکار را رد کرد");
    }

    private async Task InstallFlowAsync()
    {
        if (Installing) return;
        try
        {
            Installing = true; InstallProgress = 0;
            BashStatus = "در حال دریافت آخرین نسخه…";
            var url = await _installer.ResolveLatestUrlAsync().ConfigureAwait(true);
            AppLogger.Instance.Info("Installer", "لینک دانلود: " + url);
            var prog = new Progress<double>(v => InstallProgress = v);
            BashStatus = "در حال دانلود Git for Windows…";
            var file = await _installer.DownloadAsync(url, prog).ConfigureAwait(true);
            BashStatus = "در حال اجرای نصب‌کننده (نیاز به تأیید ادمین)…";
            _installer.RunInstall(file);
            MessageBox.Show("نصب‌کننده Git اجرا شد.\nپس از پایان نصب، دکمه «بررسی مجدد» را بزنید.",
                "نصب Git Bash", MessageBoxButton.OK, MessageBoxImage.Information);
            BashStatus = "نصب‌کننده اجرا شد — پس از نصب «بررسی مجدد» بزنید";
        }
        catch (Exception ex)
        {
            BashStatus = "خطا در نصب: " + ex.Message;
            AppLogger.Instance.Error("Installer", "خطا در دانلود/نصب Git", ex);
            MessageBox.Show("خطا در دانلود/نصب:\n" + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Installing = false; }
    }

    public async ValueTask DisposeAsync() => await Terminals.DisposeAsync().ConfigureAwait(false);
}
