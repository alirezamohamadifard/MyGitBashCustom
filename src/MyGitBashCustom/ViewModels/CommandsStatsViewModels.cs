using System.Collections.ObjectModel;
using System.Windows;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Models;
using MyGitBashCustom.Services;

namespace MyGitBashCustom.ViewModels;

public sealed class CommandsViewModel : ObservableObject
{
    private string _search = "";
    private string _category = "همه";
    public ObservableCollection<GitCommandInfo> Filtered { get; } = new();
    public List<string> Categories { get; } = ["همه", "Git", "Bash", "File", "Network", "System", "Custom"];

    public string Search { get => _search; set { if (Set(ref _search, value)) Apply(); } }
    public string Category { get => _category; set { if (Set(ref _category, value)) Apply(); } }

    public RelayCommand CopySyntaxCommand { get; }
    public RelayCommand CopyExampleCommand { get; }
    public event Action<string>? SendToTerminal;

    public CommandsViewModel()
    {
        CopySyntaxCommand = new RelayCommand(p => SafeCopy((p as GitCommandInfo)?.Syntax));
        CopyExampleCommand = new RelayCommand(p => SafeCopy((p as GitCommandInfo)?.Example));
        SendCommand = new RelayCommand(p => { if (p is GitCommandInfo c) SendToTerminal?.Invoke(c.Example); });
        Apply();
    }
    public RelayCommand SendCommand { get; }

    private static void SafeCopy(string? s) { try { if (!string.IsNullOrEmpty(s)) Clipboard.SetText(s); } catch { } }

    private void Apply()
    {
        Filtered.Clear();
        var q = Search.Trim().ToLowerInvariant();
        foreach (var c in CommandCatalog.All)
        {
            if (Category != "همه" && c.Category.ToString() != Category) continue;
            if (!string.IsNullOrEmpty(q) &&
                !c.Name.ToLowerInvariant().Contains(q) &&
                !c.DescriptionFa.Contains(Search.Trim()) &&
                !c.Syntax.ToLowerInvariant().Contains(q) &&
                !c.AliasesValues.Any(a => a.Contains(q))) continue;
            Filtered.Add(c);
        }
    }
}

public sealed class StatsViewModel : ObservableObject
{
    private readonly UsageStatsService _stats;
    public ObservableCollection<CommandUsageStat> Top { get; } = new();
    private int _total; public int Total { get => _total; set => Set(ref _total, value); }
    private string _fav = "—"; public string Favorite { get => _fav; set => Set(ref _fav, value); }

    public AsyncRelayCommand RefreshCommand { get; }
    public StatsViewModel(UsageStatsService stats)
    {
        _stats = stats;
        RefreshCommand = new AsyncRelayCommand(() => Task.Run(() => Refresh()));
    }
    public void Refresh()
    {
        var top = _stats.Top(15);
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Top.Clear(); foreach (var t in top) Top.Add(t);
            Total = _stats.TotalCount;
            Favorite = top.FirstOrDefault()?.Command ?? "—";
        });
    }
}
