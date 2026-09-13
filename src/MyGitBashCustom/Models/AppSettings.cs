namespace MyGitBashCustom.Models;

public enum AppTheme { Dark, Light, Midnight, TerminalGreen }
public enum AccentColor { Blue, Green, Purple, Orange, Pink }
public enum DataLocation { Auto, Portable, AppData, Custom }

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public AccentColor Accent { get; set; } = AccentColor.Blue;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 14;
    public bool ConfirmBeforeInstall { get; set; } = true;
    public bool AutoCheckGitBash { get; set; } = true;
    public string? CustomBashPath { get; set; }
    public bool SaveHistory { get; set; } = true;
    public int MaxHistory { get; set; } = 500;
    public bool StartMaximized { get; set; } = false;
    public bool ShowWelcome { get; set; } = true;
    public string PromptSymbol { get; set; } = "❯";
    public bool EnableSyntaxHighlight { get; set; } = true;
    public bool EnableIntelliSense { get; set; } = true;
    public bool RtlUi { get; set; } = true;
    public DataLocation DataLocation { get; set; } = DataLocation.Auto;
    public string? CustomDataPath { get; set; }
}
