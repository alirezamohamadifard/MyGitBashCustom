using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Models;
using MyGitBashCustom.Services;

namespace MyGitBashCustom.ViewModels;

public sealed record DayCount(DateTime Day, int Count);

public sealed class ActivityViewModel : ObservableObject
{
    private readonly ActivityService _svc;
    public ObservableCollection<DayCount> Days { get; } = new();
    private int _range = 30;
    public int Range { get => _range; set { if (Set(ref _range, value)) _ = LoadAsync(); } }
    private string _summary = "…"; public string Summary { get => _summary; set => Set(ref _summary, value); }
    private int _total; public int Total { get => _total; set => Set(ref _total, value); }
    private double _success; public double SuccessRate { get => _success; set => Set(ref _success, value); }
    private int _max; public int MaxDay { get => _max; set => Set(ref _max, value); }
    public AsyncRelayCommand LoadCommand { get; }

    public ActivityViewModel(ActivityService svc) { _svc = svc; LoadCommand = new AsyncRelayCommand(LoadAsync); }

    public async Task LoadAsync()
    {
        try
        {
            var map = await _svc.DailyCountsAsync(Range).ConfigureAwait(false);
            var to = DateTime.Now.Date.AddDays(1).AddTicks(-1);
            var entries = await _svc.QueryAsync(to.AddDays(-Range + 1).Date, to).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Days.Clear();
                foreach (var kv in map.OrderBy(k => k.Key)) Days.Add(new DayCount(kv.Key, kv.Value));
                Total = entries.Count;
                SuccessRate = entries.Count == 0 ? 0 : 100.0 * entries.Count(e => e.Success) / entries.Count;
                MaxDay = Days.Count == 0 ? 0 : Days.Max(d => d.Count);
                var busy = Days.OrderByDescending(d => d.Count).FirstOrDefault();
                Summary = entries.Count == 0 ? "هنوز فعالیتی ثبت نشده — یک دستور اجرا کنید."
                    : $"{PersianHelper.ToFaDigits(Total)} دستور در {PersianHelper.ToFaDigits(Range)} روز گذشته • موفقیت {PersianHelper.ToFaDigits($"{SuccessRate:0}%")} • شلوغ‌ترین روز: {(busy is null ? "—" : PersianHelper.ToPersianDate(busy.Day) + $" ({PersianHelper.ToFaDigits(busy.Count)} دستور)")}";
            }).Task.ConfigureAwait(false);
        }
        catch (Exception ex) { AppLogger.Instance.Error("Activity", "بارگذاری گزارش عملکرد ناموفق بود", ex); }
    }
}

public sealed class LogsViewModel : ObservableObject
{
    private string _search = "";
    private string _level = "همه";
    public List<string> Levels { get; } = ["همه", "Debug", "Info", "Warning", "Error", "Fatal"];
    public string Search { get => _search; set { if (Set(ref _search, value)) Apply(); } }
    public string Level { get => _level; set { if (Set(ref _level, value)) Apply(); } }
    public ObservableCollection<AppLogEntry> Filtered { get; } = new();
    public RelayCommand ClearCommand { get; }
    public AsyncRelayCommand ExportCommand { get; }

    public LogsViewModel()
    {
        ClearCommand = new RelayCommand(() => { AppLogger.Instance.ClearUi(); Apply(); });
        ExportCommand = new AsyncRelayCommand(ExportAsync);
        AppLogger.Instance.UiChanged += (_, __) => Apply();
        Apply();
    }

    private void Apply()
    {
        try
        {
            var q = Search.Trim();
            var items = AppLogger.Instance.UiLogs
                .Where(e => (Level == "همه" || e.Level.ToString() == Level))
                .Where(e => string.IsNullOrEmpty(q) || e.Message.Contains(q, StringComparison.OrdinalIgnoreCase) || e.Source.Contains(q, StringComparison.OrdinalIgnoreCase))
                .TakeLast(500).ToList();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Filtered.Clear();
                foreach (var e in items) Filtered.Add(e);
            });
        }
        catch { }
    }

    private async Task ExportAsync()
    {
        try
        {
            var dlg = new SaveFileDialog { FileName = $"mygitbash-logs-{DateTime.Now:yyyyMMdd-HHmm}.txt", Filter = "Text|*.txt" };
            if (dlg.ShowDialog() == true)
            {
                await AppLogger.Instance.ExportAsync(dlg.FileName).ConfigureAwait(true);
                MessageBox.Show("لاگ‌ها ذخیره شد:\n" + dlg.FileName, "خروجی لاگ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex) { MessageBox.Show("خطا در ذخیره: " + ex.Message); }
    }
}

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _svc;
    private readonly Func<bool> _isRunning;
    public SettingsViewModel(SettingsService svc, Func<bool> isRunning)
    {
        _svc = svc; _isRunning = isRunning;
        BrowseCommand = new RelayCommand(Browse);
        BrowseDataCommand = new RelayCommand(BrowseData);
        ResetCommand = new RelayCommand(ResetDefaults);
        SaveNote = "";
        Refresh();
        _svc.Changed += () => Applied?.Invoke();
    }
    public event Action? Applied;
    public RelayCommand BrowseCommand { get; }
    public RelayCommand BrowseDataCommand { get; }
    public RelayCommand ResetCommand { get; }

    private void Browse()
    {
        var dlg = new OpenFileDialog { Filter = "bash.exe|bash.exe", FileName = "bash.exe" };
        if (dlg.ShowDialog() == true)
        {
            BashPath = dlg.FileName;
            AppLogger.Instance.Info("Settings", "مسیر دستی Bash انتخاب شد: " + dlg.FileName);
        }
    }

    private void BrowseData()
    {
        var dlg = new OpenFolderDialog { Title = "انتخاب پوشه ذخیره داده‌ها" };
        if (dlg.ShowDialog() == true)
        {
            CustomDataPath = dlg.FolderName;
            DataLocation = DataLocation.Custom;
        }
    }

    public void Refresh()
    {
        var s = _svc.Current;
        Theme = s.Theme; Accent = s.Accent; FontFamily = s.FontFamily; FontSize = s.FontSize;
        ConfirmBeforeInstall = s.ConfirmBeforeInstall; AutoCheck = s.AutoCheckGitBash;
        BashPath = s.CustomBashPath ?? ""; SaveHistory = s.SaveHistory; MaxHistory = s.MaxHistory;
        StartMaximized = s.StartMaximized; ShowWelcome = s.ShowWelcome; Prompt = s.PromptSymbol;
        Highlight = s.EnableSyntaxHighlight; Intel = s.EnableIntelliSense;
        _dataLoc = s.DataLocation; OnPropertyChanged(nameof(DataLocation));
        _customData = s.CustomDataPath ?? ""; OnPropertyChanged(nameof(CustomDataPath));
        OnPropertyChanged(nameof(CurrentDataRoot));
        OnPropertyChanged(nameof(IsCustomData));
    }

    private AppTheme _theme; public AppTheme Theme { get => _theme; set { if (Set(ref _theme, value)) Save(); } }
    private AccentColor _accent; public AccentColor Accent { get => _accent; set { if (Set(ref _accent, value)) Save(); } }
    private string _ff = "Segoe UI"; public string FontFamily { get => _ff; set { if (Set(ref _ff, value)) Save(); } }
    private double _fs = 14; public double FontSize { get => _fs; set { if (Set(ref _fs, value)) Save(); } }
    private bool _cbi; public bool ConfirmBeforeInstall { get => _cbi; set { if (Set(ref _cbi, value)) Save(); } }
    private bool _auto; public bool AutoCheck { get => _auto; set { if (Set(ref _auto, value)) Save(); } }
    private string _bp = ""; public string BashPath { get => _bp; set { if (Set(ref _bp, value)) Save(); } }
    private bool _sh; public bool SaveHistory { get => _sh; set { if (Set(ref _sh, value)) Save(); } }
    private int _mh = 500; public int MaxHistory { get => _mh; set { if (Set(ref _mh, value)) Save(); } }
    private bool _sm; public bool StartMaximized { get => _sm; set { if (Set(ref _sm, value)) Save(); } }
    private bool _sw = true; public bool ShowWelcome { get => _sw; set { if (Set(ref _sw, value)) Save(); } }
    private string _pr = "❯"; public string Prompt { get => _pr; set { if (Set(ref _pr, value)) Save(); } }
    private bool _hl = true; public bool Highlight { get => _hl; set { if (Set(ref _hl, value)) Save(); } }
    private bool _in = true; public bool Intel { get => _in; set { if (Set(ref _in, value)) Save(); } }
    private string _note = ""; public string SaveNote { get => _note; set => Set(ref _note, value); }

    public List<AppTheme> Themes { get; } = [AppTheme.Dark, AppTheme.Light, AppTheme.Midnight, AppTheme.TerminalGreen];
    public List<AccentColor> Accents { get; } = [AccentColor.Blue, AccentColor.Green, AccentColor.Purple, AccentColor.Orange, AccentColor.Pink];
    public List<string> Fonts { get; } = ["Segoe UI", "Tahoma", "Consolas", "Cascadia Code", "Vazirmatn"];

    private void ResetDefaults()
    {
        var d = new AppSettings();
        _svc.Update(s =>
        {
            s.Theme = d.Theme; s.Accent = d.Accent; s.FontFamily = d.FontFamily; s.FontSize = d.FontSize;
            s.ConfirmBeforeInstall = d.ConfirmBeforeInstall; s.AutoCheckGitBash = d.AutoCheckGitBash;
            s.CustomBashPath = d.CustomBashPath; s.SaveHistory = d.SaveHistory; s.MaxHistory = d.MaxHistory;
            s.StartMaximized = d.StartMaximized; s.ShowWelcome = d.ShowWelcome; s.PromptSymbol = d.PromptSymbol;
            s.EnableSyntaxHighlight = d.EnableSyntaxHighlight; s.EnableIntelliSense = d.EnableIntelliSense;
            s.RtlUi = d.RtlUi; s.DataLocation = d.DataLocation; s.CustomDataPath = d.CustomDataPath;
        });
        Refresh();
        SaveNote = "✓ همهٔ تنظیمات به حالت پیش‌فرض بازگشت";
        Applied?.Invoke();
    }

    private void Save()
    {
        _svc.Update(s =>
        {
            s.Theme = Theme; s.Accent = Accent; s.FontFamily = FontFamily; s.FontSize = FontSize;
            s.ConfirmBeforeInstall = ConfirmBeforeInstall; s.AutoCheckGitBash = AutoCheck;
            s.CustomBashPath = string.IsNullOrWhiteSpace(BashPath) ? null : BashPath;
            s.SaveHistory = SaveHistory; s.MaxHistory = MaxHistory; s.StartMaximized = StartMaximized;
            s.ShowWelcome = ShowWelcome; s.PromptSymbol = string.IsNullOrWhiteSpace(Prompt) ? "❯" : Prompt;
            s.EnableSyntaxHighlight = Highlight; s.EnableIntelliSense = Intel;
            s.DataLocation = DataLocation;
            s.CustomDataPath = string.IsNullOrWhiteSpace(CustomDataPath) ? null : CustomDataPath;
        });
        SaveNote = "✓ تغییرات اعمال و ذخیره شد — " + DateTime.Now.ToString("HH:mm:ss");
        OnPropertyChanged(nameof(SaveNote));
        OnPropertyChanged(nameof(CurrentDataRoot));
    }

    private DataLocation _dataLoc = DataLocation.Auto;
    public DataLocation DataLocation
    {
        get => _dataLoc;
        set
        {
            if (Set(ref _dataLoc, value))
            {
                Save();
                SaveNote = "✓ محل داده پس از راه‌اندازی مجدد اعمال می‌شود";
                OnPropertyChanged(nameof(SaveNote));
                OnPropertyChanged(nameof(IsCustomData));
            }
        }
    }
    private string _customData = "";
    public string CustomDataPath
    {
        get => _customData;
        set { if (Set(ref _customData, value)) { Save(); OnPropertyChanged(nameof(IsCustomData)); } }
    }
    public bool IsCustomData => DataLocation == DataLocation.Custom;
    public string CurrentDataRoot => AppPaths.Root + (AppPaths.IsPortable ? "  (کنار برنامه ✓)" : "  (AppData)");
    public List<DataLocation> DataLocations { get; } = [DataLocation.Auto, DataLocation.Portable, DataLocation.AppData, DataLocation.Custom];

    public string EngineState => _isRunning() ? "موتور روشن است" : "موتور خاموش است";
    public string DataFolder => AppPaths.Root;
    public void OpenDataFolder() { try { System.Diagnostics.Process.Start("explorer.exe", AppPaths.Root); } catch { } }
}
