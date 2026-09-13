using System.IO;
using System.Text.Json;
using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>آمار پُراستفاده‌ترین دستورات + تاریخچه. ذخیره JSON با debounce.</summary>
public sealed class UsageStatsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly Dictionary<string, CommandUsageStat> _map = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _history = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _saveCts;

    public IReadOnlyDictionary<string, CommandUsageStat> Snapshot()
    {
        lock (_map) return new Dictionary<string, CommandUsageStat>(_map, StringComparer.OrdinalIgnoreCase);
    }
    public IReadOnlyList<string> HistorySnapshot()
    {
        lock (_history) return _history.ToList();
    }

    public static string BaseOf(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return "";
        var t = command.Trim().TrimStart();
        // sudo X -> X
        if (t.StartsWith("sudo ", StringComparison.OrdinalIgnoreCase)) t = t[5..].TrimStart();
        var first = t.Split([' ', '\t', ';', '|', '&'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (first.Equals("git", StringComparison.OrdinalIgnoreCase))
        {
            var parts = t.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && !parts[1].StartsWith('-')) return "git " + parts[1].ToLowerInvariant();
            return "git";
        }
        return first.ToLowerInvariant();
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(AppPaths.StatsFile))
            {
                var json = await File.ReadAllTextAsync(AppPaths.StatsFile).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<CommandUsageStat>>(json) ?? [];
                lock (_map) { _map.Clear(); foreach (var s in list) _map[s.Command] = s; }
            }
            if (File.Exists(AppPaths.HistoryFile))
            {
                var lines = await File.ReadAllLinesAsync(AppPaths.HistoryFile).ConfigureAwait(false);
                lock (_history) { _history.Clear(); _history.AddRange(lines.Where(l => !string.IsNullOrWhiteSpace(l)).TakeLast(500)); }
            }
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Stats", "بارگذاری آمار ناموفق بود", ex); }
    }

    public void Record(string fullCommand, bool success, long ms)
    {
        var b = BaseOf(fullCommand);
        if (string.IsNullOrEmpty(b)) return;
        lock (_map)
        {
            if (!_map.TryGetValue(b, out var s)) { s = new CommandUsageStat { Command = b }; _map[b] = s; }
            s.Count++; s.TotalMs += ms; s.LastUsed = DateTime.Now;
            if (success) s.SuccessCount++; else s.FailCount++;
        }
        lock (_history)
        {
            _history.Remove(fullCommand);
            _history.Add(fullCommand);
            while (_history.Count > 500) _history.RemoveAt(0);
        }
        DebouncedSave();
    }

    private void DebouncedSave()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var t = _saveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1200, t).ConfigureAwait(false);
                await SaveNowAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { AppLogger.Instance.Warn("Stats", "ذخیره آمار ناموفق بود", ex); }
        }, t);
    }

    public async Task SaveNowAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            List<CommandUsageStat> list;
            List<string> hist;
            lock (_map) list = _map.Values.ToList();
            lock (_history) hist = _history.ToList();
            await File.WriteAllTextAsync(AppPaths.StatsFile, JsonSerializer.Serialize(list, Json)).ConfigureAwait(false);
            await File.WriteAllLinesAsync(AppPaths.HistoryFile, hist).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public List<CommandUsageStat> Top(int n = 10)
    {
        lock (_map) return _map.Values.OrderByDescending(s => s.Count).Take(n).ToList();
    }
    public int TotalCount { get { lock (_map) return _map.Values.Sum(s => s.Count); } }
}
