using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MyGitBashCustom.Helpers;

public sealed class BoolToVis : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class InvBoolToVis : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => v is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class CountBarWidth : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        int n = v is int i ? i : 0;
        return Math.Clamp(10 + n * 12.0, 10.0, 230.0);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class CountBarHeight : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        int n = v is int i ? i : 0;
        if (n <= 0) return 4.0;
        return Math.Min(140.0, 12 + n * 9.0);
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class StringToVis : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(v as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
public sealed class LevelBrush : IValueConverter
{
    public object Convert(object v, Type t, object p, CultureInfo c)
    {
        var s = v?.ToString() ?? "";
        return new SolidColorBrush(s switch
        {
            "Error" => (Color)ColorConverter.ConvertFromString("#F85149"),
            "Fatal" => (Color)ColorConverter.ConvertFromString("#FF2D55"),
            "Warning" => (Color)ColorConverter.ConvertFromString("#D29922"),
            "Info" => (Color)ColorConverter.ConvertFromString("#58A6FF"),
            _ => (Color)ColorConverter.ConvertFromString("#8B949E"),
        });
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
