using System.IO;
using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>
/// موتور اینتلیسنس: ترکیب پیشوند + فازی + تاریخچه + مسیر فایل.
/// </summary>
public sealed class IntelliSenseEngine
{
    private readonly Func<IReadOnlyList<string>> _historyProvider;
    private readonly Func<string> _cwdProvider;
    private readonly Func<IReadOnlyDictionary<string, CommandUsageStat>> _statsProvider;

    public IntelliSenseEngine(Func<IReadOnlyList<string>> history, Func<string> cwd, Func<IReadOnlyDictionary<string, CommandUsageStat>> stats)
    { _historyProvider = history; _cwdProvider = cwd; _statsProvider = stats; }

    public List<SuggestionItem> Suggest(string input, int max = 9)
    {
        input ??= "";
        var caret = input.TrimStart();
        var results = new List<SuggestionItem>();
        if (string.IsNullOrWhiteSpace(caret)) return TopHistory(max);

        var parts = SplitSmart(caret);
        bool endsSpace = caret.EndsWith(' ');
        string current = endsSpace ? "" : parts.LastOrDefault() ?? "";

        // حالت ۱: تکمیل نام دستور (کلمه اول)
        if (parts.Length <= 1 && !endsSpace)
        {
            var q = current.ToLowerInvariant();
            foreach (var c in CommandCatalog.All)
            {
                double s = Score(c.Name, q);
                if (s > 0.25)
                {
                    BoostByStats(c.Name.Split(' ')[0], ref s);
                    results.Add(new SuggestionItem { Text = c.Name, Display = c.Name, Description = c.DescriptionFa, Kind = "دستور", Score = s + 0.35 });
                }
                foreach (var a in c.AliasesValues)
                {
                    double sa = Score(a, q);
                    if (sa > 0.5) results.Add(new SuggestionItem { Text = a, Display = $"{a}  ← {c.Name}", Description = c.DescriptionFa, Kind = "مستعار", Score = sa });
                }
            }
            // تاریخچه
            foreach (var h in _historyProvider())
            {
                if (h.StartsWith(caret, StringComparison.OrdinalIgnoreCase) && h.Length > caret.Length)
                    results.Add(new SuggestionItem { Text = h, Display = h, Description = "از تاریخچه شما", Kind = "تاریخچه", Score = 0.9 });
            }
        }
        // حالت ۲: زیر‌دستور git
        else if (parts.Length >= 1 && parts[0].Equals("git", StringComparison.OrdinalIgnoreCase) && (parts.Length == 1 || (parts.Length == 2 && !endsSpace)))
        {
            var q = (endsSpace ? "" : parts[1]).ToLowerInvariant();
            foreach (var c in CommandCatalog.All.Where(x => x.Name.StartsWith("git ", StringComparison.OrdinalIgnoreCase)))
            {
                var sub = c.Name["git ".Length..];
                if (sub.StartsWith(q, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(q))
                    results.Add(new SuggestionItem { Text = "git " + sub, Display = "git " + sub, Description = $"{c.DescriptionFa} — {c.Syntax}", Kind = "زیر‌دستور git", Score = 1.2 - sub.Length * 0.01 });
            }
        }
        // حالت ۳: پرچم‌ها
        else if (current.StartsWith("--") || (current.StartsWith("-") && current.Length <= 3))
        {
            foreach (var f in CommonFlags(parts[0], current))
                results.Add(f);
        }
        // حالت ۴: مسیر فایل
        else
        {
            foreach (var p in SuggestPaths(current, max)) results.Add(p);
            // پرچم‌های مرتبط هم نشان بده
            if (string.IsNullOrEmpty(current))
                foreach (var f in CommonFlags(parts[0], "").Take(3)) results.Add(f);
        }

        return results
            .GroupBy(r => r.Text).Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(r => r.Score).Take(max).ToList();
    }

    private List<SuggestionItem> TopHistory(int max)
    {
        var stats = _statsProvider();
        var list = new List<SuggestionItem>();
        foreach (var h in _historyProvider().Distinct().Take(30))
            list.Add(new SuggestionItem { Text = h, Display = h, Description = "تاریخچه اخیر", Kind = "تاریخچه", Score = 0.5 });
        foreach (var kv in stats.OrderByDescending(k => k.Value.Count).Take(5))
            list.Add(new SuggestionItem { Text = kv.Key, Display = kv.Key + $"  (×{kv.Value.Count})", Description = "پُراستفاده شما", Kind = "پُراستفاده", Score = 1.0 });
        return list.GroupBy(x => x.Text).Select(g => g.First()).Take(max).ToList();
    }

    private void BoostByStats(string baseCmd, ref double s)
    {
        var stats = _statsProvider();
        if (stats.TryGetValue(baseCmd, out var st)) s += Math.Min(0.4, st.Count * 0.02);
    }

    private static string[] SplitSmart(string s)
        => s.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

    private static double Score(string target, string q)
    {
        if (string.IsNullOrEmpty(q)) return 0.4;
        target = target.ToLowerInvariant();
        if (target.Equals(q)) return 2.0;
        if (target.StartsWith(q)) return 1.5 - target.Length * 0.005;
        if (target.Contains(q)) return 0.8;
        // فازی: زیر‌دنباله
        int ti = 0, matched = 0;
        foreach (var ch in q)
        {
            int f = target.IndexOf(ch, ti);
            if (f < 0) return 0;
            matched++; ti = f + 1;
        }
        return 0.3 + 0.2 * matched / Math.Max(1, q.Length);
    }

    private static IEnumerable<SuggestionItem> CommonFlags(string cmd, string prefix)
    {
        string[] gitFlags = ["--help", "--oneline", "--graph", "--all", "-v", "-m", "-a", "-f", "--force", "--staged", "--global"];
        string[] lsFlags = ["-la", "-l", "-a", "-h", "--color"];
        string[] grepFlags = ["-r", "-n", "-i", "--color", "-E"];
        string[] arr = cmd.Contains("git") ? gitFlags : cmd is "ls" or "ll" ? lsFlags : cmd is "grep" ? grepFlags : ["--help", "-v", "-f"];
        foreach (var f in arr)
            if (f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                yield return new SuggestionItem { Text = f, Display = f, Description = "پرچم " + cmd, Kind = "پرچم", Score = 0.7 };
    }

    private List<SuggestionItem> SuggestPaths(string partial, int max)
    {
        var list = new List<SuggestionItem>();
        try
        {
            var cwd = _cwdProvider();
            if (!Directory.Exists(cwd)) return list;
            string dir = cwd, pat = partial;
            if (!string.IsNullOrEmpty(partial))
            {
                var combined = Path.IsPathRooted(partial) ? partial : Path.Combine(cwd, partial);
                dir = Path.GetDirectoryName(combined) ?? cwd;
                pat = Path.GetFileName(combined) ?? "";
                if (partial.EndsWith('/') || partial.EndsWith('\\')) { dir = combined; pat = ""; }
            }
            if (!Directory.Exists(dir)) return list;
            var opts = new EnumerationOptions { IgnoreInaccessible = true };
            foreach (var d in Directory.EnumerateDirectories(dir, (string.IsNullOrEmpty(pat) ? "*" : pat + "*"), opts).Take(max))
                list.Add(new SuggestionItem { Text = Rel(cwd, d) + "/", Display = "📁 " + Path.GetFileName(d) + "/", Description = "پوشه", Kind = "مسیر", Score = 0.6 });
            foreach (var f in Directory.EnumerateFiles(dir, (string.IsNullOrEmpty(pat) ? "*" : pat + "*"), opts).Take(max))
                list.Add(new SuggestionItem { Text = Rel(cwd, f), Display = "📄 " + Path.GetFileName(f), Description = "فایل", Kind = "مسیر", Score = 0.55 });
        }
        catch { }
        return list;
    }

    private static string Rel(string cwd, string full)
    {
        try { return Path.GetRelativePath(cwd, full).Replace('\\', '/'); }
        catch { return full; }
    }
}
