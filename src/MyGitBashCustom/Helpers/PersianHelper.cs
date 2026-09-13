using System.Globalization;

namespace MyGitBashCustom.Helpers;

/// <summary>کمک‌کننده تاریخ شمسی و قالب‌بندی فارسی.</summary>
public static class PersianHelper
{
    private static readonly PersianCalendar Pc = new();
    private static readonly string[] FaDigits = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"];

    public static string ToFaDigits(string s)
    {
        for (int i = 0; i <= 9; i++) s = s.Replace(i.ToString(), FaDigits[i]);
        return s;
    }
    public static string ToFaDigits(int n) => ToFaDigits(n.ToString());
    public static string ToFaDigits(long n) => ToFaDigits(n.ToString());

    public static string ToPersianDate(DateTime dt)
        => ToFaDigits($"{Pc.GetYear(dt):0000}/{Pc.GetMonth(dt):00}/{Pc.GetDayOfMonth(dt):00}");

    public static string ToPersianDateTime(DateTime dt)
        => ToFaDigits($"{Pc.GetYear(dt):0000}/{Pc.GetMonth(dt):00}/{Pc.GetDayOfMonth(dt):00} {dt:HH:mm}");

    public static string RelativeFa(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "لحظاتی پیش";
        if (span.TotalMinutes < 60) return ToFaDigits($"{(int)span.TotalMinutes} دقیقه پیش");
        if (span.TotalHours < 24) return ToFaDigits($"{(int)span.TotalHours} ساعت پیش");
        if (span.TotalDays < 30) return ToFaDigits($"{(int)span.TotalDays} روز پیش");
        return ToPersianDate(dt);
    }

    public static string FormatDuration(long ms)
    {
        if (ms < 1000) return ToFaDigits($"{ms} م.ث");
        if (ms < 60_000) return ToFaDigits($"{ms / 1000.0:0.0} ثانیه");
        return ToFaDigits($"{ms / 60000.0:0.0} دقیقه");
    }
}
