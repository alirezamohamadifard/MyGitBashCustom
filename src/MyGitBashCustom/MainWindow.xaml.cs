using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Navigation;
using MyGitBashCustom.ViewModels;
using MyGitBashCustom.Views;

namespace MyGitBashCustom;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(App.Settings, App.Stats, App.Activity);
        DataContext = _vm;
        _vm.RenameTabRequested += OnRenameTabRequested;

        RangeBox.SelectionChanged += (_, __) =>
        {
            _vm.Activity.Range = RangeBox.SelectedIndex switch { 0 => 7, 2 => 90, _ => 30 };
        };
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (App.Settings.Current.StartMaximized) WindowState = WindowState.Maximized;
        _vm.Stats.Refresh();
        await _vm.Activity.LoadAsync().ConfigureAwait(true);
        await _vm.CheckAsync(true).ConfigureAwait(true);
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        try { await _vm.DisposeAsync().ConfigureAwait(false); } catch { }
    }

    private void OnRenameTabRequested(TerminalViewModel tab)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var name = InputDialog.Show(this, "تغییر نام تب", "نام جدید تب:", tab.Title);
            if (name is not null)
            {
                tab.Title = name;
                Services.AppLogger.Instance.Info("Tabs", $"تب به «{name}» تغییر نام داد");
            }
        });
    }

    private void TabHeader_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TabItem { DataContext: TerminalViewModel tab })
            _vm.Terminals.RenameTabCommand.Execute(tab);
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
        => _vm.SettingsVm.OpenDataFolder();

    private void SupportEmail_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Services.AppLogger.Instance.Warn("Support", "بازکردن برنامه ایمیل ناموفق بود", ex);
            Clipboard.SetText("realmadrid121925@outlook.com");
            MessageBox.Show("برنامه ایمیل باز نشد؛ نشانی ایمیل در کلیپ‌بورد کپی شد.", "ارتباط با ما", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
