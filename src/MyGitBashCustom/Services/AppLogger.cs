using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>
/// لاگر حرفه‌ای thread-safe: فایل چرخشی روزانه + بافر حافظه برای UI. Singleton.
/// </summary>
public sealed class AppLogger
{
    public static AppLogger Instance { get; } = new();
    private readonly ConcurrentQueue<AppLogEntry> _queue = new();
    private readonly object _fileLock = new();
    private readonly ObservableCollection<AppLogEntry> _ui = new();
    private System.Windows.Threading.Dispatcher? _dispatcher;

    public ReadOnlyObservableCollection<AppLogEntry> UiLogs { get; }
    public event NotifyCollectionChangedEventHandler? UiChanged;

    private AppLogger()
    {
        UiLogs = new ReadOnlyObservableCollection<AppLogEntry>(_ui);
        ((INotifyCollectionChanged)_ui).CollectionChanged += (_, e) => UiChanged?.Invoke(this, e);
    }

    public void AttachDispatcher(System.Windows.Threading.Dispatcher d) => _dispatcher = d;

    public void Log(LogLevel level, string source, string message, Exception? ex = null)
    {
        var e = new AppLogEntry { Level = level, Source = source, Message = message, Exception = ex?.ToString() };
        _queue.Enqueue(e);
        while (_queue.Count > 5000 && _queue.TryDequeue(out _)) { }
        try
        {
            void add()
            {
                _ui.Add(e);
                while (_ui.Count > 2000) _ui.RemoveAt(0);
            }
            if (_dispatcher is null || _dispatcher.CheckAccess()) add();
            else _dispatcher.BeginInvoke(add);
        }
        catch { /* لاگ نباید اپ را بترکاند */ }
        _ = Task.Run(() => WriteFile(e));
    }

    private void WriteFile(AppLogEntry e)
    {
        try
        {
            lock (_fileLock)
            {
                var path = Path.Combine(AppPaths.LogsDir, $"app-{DateTime.Now:yyyy-MM-dd}.log");
                var sb = new StringBuilder();
                sb.Append($"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level,-7}] [{e.Source}] {e.Message}");
                if (e.Exception is not null) sb.AppendLine().Append(e.Exception);
                sb.AppendLine();
                File.AppendAllText(path, sb.ToString(), Encoding.UTF8);
                Cleanup();
            }
        }
        catch { }
    }

    private void Cleanup()
    {
        try
        {
            foreach (var f in new DirectoryInfo(AppPaths.LogsDir).GetFiles("app-*.log")
                         .OrderByDescending(f => f.Name).Skip(14))
                f.Delete();
        }
        catch { }
    }

    public async Task<string> ExportAsync(string destPath)
    {
        var sb = new StringBuilder();
        foreach (var e in _queue.ToArray().OrderBy(x => x.Timestamp))
        {
            sb.AppendLine($"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.Level}] [{e.Source}] {e.Message}");
            if (e.Exception is not null) sb.AppendLine(e.Exception);
        }
        await File.WriteAllTextAsync(destPath, sb.ToString(), Encoding.UTF8).ConfigureAwait(false);
        return destPath;
    }

    public void ClearUi()
    {
        void c() => _ui.Clear();
        if (_dispatcher is null || _dispatcher.CheckAccess()) c(); else _dispatcher.BeginInvoke(c);
    }

    // شورت‌کات‌ها
    public void Debug(string s, string m) => Log(LogLevel.Debug, s, m);
    public void Info(string s, string m) => Log(LogLevel.Info, s, m);
    public void Warn(string s, string m, Exception? e = null) => Log(LogLevel.Warning, s, m, e);
    public void Error(string s, string m, Exception? e = null) => Log(LogLevel.Error, s, m, e);
    public void Fatal(string s, string m, Exception? e = null) => Log(LogLevel.Fatal, s, m, e);
}
