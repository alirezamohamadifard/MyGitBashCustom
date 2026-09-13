using System.Collections.ObjectModel;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Services;

namespace MyGitBashCustom.ViewModels;

/// <summary>
/// مدیر تب‌های بی‌نهایت ترمینال؛ هر تب موتور Bash مستقل خودش را دارد.
/// </summary>
public sealed class TerminalManagerViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Func<GitBashEngine> _engineFactory;
    private readonly UsageStatsService _stats;
    private readonly ActivityService _activity;
    private readonly SettingsService _settings;

    private TerminalViewModel? _selected;
    private int _counter;
    private bool _welcomed;
    private string? _bashPath;
    private string _gitVer = "";
    private string _bashVer = "";

    public ObservableCollection<TerminalViewModel> Tabs { get; } = new();

    public TerminalViewModel? Selected
    {
        get => _selected;
        set => Set(ref _selected, value);
    }

    public bool IsReady => _bashPath is not null;
    public bool AnyRunning => Tabs.Any(t => t.EngineReady);

    public AsyncRelayCommand AddTabCommand { get; }
    public RelayCommand CloseTabCommand { get; }
    public RelayCommand DuplicateTabCommand { get; }
    public RelayCommand CloseOthersCommand { get; }
    public RelayCommand RenameTabCommand { get; }
    public RelayCommand NextTabCommand { get; }
    public RelayCommand PrevTabCommand { get; }

    public event Action<TerminalViewModel>? RenameRequested;

    public TerminalManagerViewModel(
        Func<GitBashEngine> engineFactory,
        UsageStatsService stats,
        ActivityService activity,
        SettingsService settings)
    {
        _engineFactory = engineFactory;
        _stats = stats; _activity = activity; _settings = settings;

        // ObservableCollection باید فقط روی Dispatcher/UI thread تغییر کند.
        AddTabCommand = new AsyncRelayCommand(() => { AddTab(); return Task.CompletedTask; });
        CloseTabCommand = new RelayCommand(p => CloseTab(p as TerminalViewModel ?? Selected));
        DuplicateTabCommand = new RelayCommand(p => DuplicateTab(p as TerminalViewModel ?? Selected));
        CloseOthersCommand = new RelayCommand(p => CloseOthers(p as TerminalViewModel ?? Selected));
        RenameTabCommand = new RelayCommand(p => { var t = p as TerminalViewModel ?? Selected; if (t is not null) RenameRequested?.Invoke(t); });
        NextTabCommand = new RelayCommand(MoveNext);
        PrevTabCommand = new RelayCommand(MovePrev);

        AddTab(select: true);
    }

    public TerminalViewModel AddTab(bool select = true)
    {
        var vm = new TerminalViewModel(_engineFactory(), _stats, _activity, _settings)
        {
            Title = $"ترمینال {++_counter}"
        };
        Tabs.Add(vm);
        if (IsReady)
            _ = vm.StartAsync(_bashPath!, welcome: false, _gitVer, _bashVer);
        if (select) Selected = vm;
        return vm;
    }

    public void CloseTab(TerminalViewModel? tab)
    {
        if (tab is null) return;
        int idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        _ = tab.DisposeEngineAsync();
        if (Selected == tab)
            Selected = Tabs.Count == 0 ? null : Tabs[Math.Clamp(idx, 0, Tabs.Count - 1)];
        if (Tabs.Count == 0) AddTab(select: true);
    }

    public void DuplicateTab(TerminalViewModel? tab)
    {
        if (tab is null) return;
        var copy = AddTab(select: true);
        copy.Title = tab.Title + " Ⅱ";
        copy.PrintLine($"▸ تکثیر از «{tab.Title}» — تاریخچه و آمار مشترک است.");
    }

    public void CloseOthers(TerminalViewModel? keep)
    {
        if (keep is null) return;
        foreach (var t in Tabs.Where(t => t != keep).ToList())
        {
            Tabs.Remove(t);
            _ = t.DisposeEngineAsync();
        }
        Selected = keep;
    }

    private void MoveNext()
    {
        if (Tabs.Count < 2 || Selected is null) return;
        Selected = Tabs[(Tabs.IndexOf(Selected) + 1) % Tabs.Count];
    }

    private void MovePrev()
    {
        if (Tabs.Count < 2 || Selected is null) return;
        Selected = Tabs[(Tabs.IndexOf(Selected) - 1 + Tabs.Count) % Tabs.Count];
    }

    /// <summary>روشن‌کردن موتور همه تب‌ها پس از آماده‌شدن Git Bash.</summary>
    public async Task EnsureStartedAsync(string bashPath, string gitVer, string bashVer)
    {
        bool first = !IsReady;
        _bashPath = bashPath; _gitVer = gitVer; _bashVer = bashVer;
        OnPropertyChanged(nameof(IsReady));
        if (first)
        {
            foreach (var t in Tabs.ToList())
            {
                bool welcome = !_welcomed;
                await t.StartAsync(bashPath, welcome, gitVer, bashVer).ConfigureAwait(true);
                if (welcome) _welcomed = true;
            }
        }
        else
        {
            foreach (var t in Tabs.Where(t => !t.EngineReady).ToList())
                await t.StartAsync(bashPath, false, gitVer, bashVer).ConfigureAwait(true);
        }
        OnPropertyChanged(nameof(AnyRunning));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var t in Tabs.ToList())
            try { await t.DisposeEngineAsync().ConfigureAwait(false); } catch { }
    }
}
