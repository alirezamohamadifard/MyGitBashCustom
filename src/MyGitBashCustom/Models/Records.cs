namespace MyGitBashCustom.Models;

public sealed class CommandUsageStat
{
    public string Command { get; set; } = "";
    public int Count { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public long TotalMs { get; set; }
    public DateTime LastUsed { get; set; }
    public double AvgMs => Count == 0 ? 0 : (double)TotalMs / Count;
    public double SuccessRate => Count == 0 ? 0 : 100.0 * SuccessCount / Count;
}

public sealed class ActivityEntry
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = "";
    public string BaseCommand { get; set; } = "";
    public int ExitCode { get; set; }
    public long DurationMs { get; set; }
    public string WorkDir { get; set; } = "";
    public bool Success => ExitCode == 0;
}

public enum LogLevel { Debug, Info, Warning, Error, Fatal }

public sealed class AppLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
}

public sealed class SuggestionItem
{
    public string Text { get; set; } = "";
    public string Display { get; set; } = "";
    public string Description { get; set; } = "";
    public string Kind { get; set; } = ""; // دستور / پرچم / مسیر / تاریخچه
    public double Score { get; set; }
}

public sealed class TerminalLine
{
    public string Text { get; set; } = "";
    public bool IsCommand { get; set; }
    public bool IsError { get; set; }
    public bool IsSuccess { get; set; }
    public DateTime Time { get; set; } = DateTime.Now;
}
