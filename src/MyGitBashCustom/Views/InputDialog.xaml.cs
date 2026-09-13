using System.Windows;

namespace MyGitBashCustom.Views;

public partial class InputDialog : Window
{
    public string Value { get; private set; } = "";

    private InputDialog(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        Lbl.Text = label;
        Box.Text = initial;
        Box.SelectAll();
        Loaded += (_, __) => Box.Focus();
        Box.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { Ok_Click(this, new RoutedEventArgs()); }
            else if (e.Key == System.Windows.Input.Key.Escape) { DialogResult = false; }
        };
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Value = Box.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public static string? Show(Window owner, string title, string label, string initial)
    {
        var d = new InputDialog(title, label, initial) { Owner = owner };
        return d.ShowDialog() == true && !string.IsNullOrWhiteSpace(d.Value) ? d.Value : null;
    }
}
