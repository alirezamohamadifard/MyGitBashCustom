using System.IO;
using System.Text.Json;
using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>لاگ عملکرد کاربر: هر اجرای دستور + گزارش ۳۰ روزه برای نمودار.</summary>
public sealed class ActivityService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static string FileFor(DateTime d) => Path.Combine(AppPaths.ActivityDir, $"activity-{d:yyyy-MM}.jsonl");

    public async Task AppendAsync(ActivityEntry e)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await File.AppendAllTextAsync(FileFor(e.Timestamp), JsonSerializer.Serialize(e, Json) + "\n").ConfigureAwait(false); }
        catch (Exception ex) { AppLogger.Instance.Warn("Activity", "ثبت فعالیت ناموفق بود", ex); }
        finally { _gate.Release(); }
    }

    public async Task<List<ActivityEntry>> QueryAsync(DateTime from, DateTime to)
    {
        var list = new List<ActivityEntry>();
        try
        {
            var months = new HashSet<string>();
            for (var d = new DateTime(from.Year, from.Month, 1); d <= to; d = d.AddMonths(1))
                months.Add(FileFor(d));
            foreach (var f in months)
            {
                if (!File.Exists(f)) continue;
                foreach (var line in await File.ReadAllLinesAsync(f).ConfigureAwait(false))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var e = JsonSerializer.Deserialize<ActivityEntry>(line);
                        if (e is not null && e.Timestamp >= from && e.Timestamp <= to) list.Add(e);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Activity", "خواندن فعالیت ناموفق بود", ex); }
        return list.OrderBy(e => e.Timestamp).ToList();
    }

    public async Task<Dictionary<DateTime, int>> DailyCountsAsync(int days = 30)
    {
        var to = DateTime.Now.Date.AddDays(1).AddTicks(-1);
        var from = DateTime.Now.Date.AddDays(-days + 1);
        var entries = await QueryAsync(from, to).ConfigureAwait(false);
        var map = Enumerable.Range(0, days).ToDictionary(i => from.Date.AddDays(i), _ => 0);
        foreach (var e in entries)
        {
            var d = e.Timestamp.Date;
            if (map.ContainsKey(d)) map[d]++;
        }
        return map;
    }
}
