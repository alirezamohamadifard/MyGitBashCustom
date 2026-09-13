using System.IO;
using System.Text.Json;
using MyGitBashCustom.Models;

namespace MyGitBashCustom.Services;

/// <summary>سرویس تنظیمات با ذخیره Debounced و پیش‌فرض‌های امن.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _saveCts;
    public AppSettings Current { get; private set; } = new();
    public event Action? Changed;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = await File.ReadAllTextAsync(AppPaths.SettingsFile).ConfigureAwait(false);
                Current = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex) { AppLogger.Instance.Error("Settings", "خطا در خواندن تنظیمات", ex); }
    }

    public void RequestSave()
    {
        _saveCts?.Cancel();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token).ConfigureAwait(false);
                await SaveNowAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { AppLogger.Instance.Error("Settings", "خطا در ذخیره تنظیمات", ex); }
        }, token);
    }

    public async Task SaveNowAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await File.WriteAllTextAsync(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Json)).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public void Update(Action<AppSettings> mut)
    {
        mut(Current);
        Changed?.Invoke();
        RequestSave();
    }
}
