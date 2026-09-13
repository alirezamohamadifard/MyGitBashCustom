using System.Windows.Controls;
using System.Windows.Documents;

namespace MyGitBashCustom.Helpers;

/// <summary>
/// مدیریت مکان‌نما بر اساس «ایندکس کاراکتر» متن ساده، مستقل از ساختار Runها.
/// بازسازی رنگ‌بندی، Runها را می‌شکند؛ آفست TextPointer در آن حالت جابه‌جا می‌شود،
/// ولی ایندکس کاراکتری همیشه سر جایش برمی‌گردد.
/// </summary>
public static class RichTextCaret
{
    public static string GetPlainText(RichTextBox box)
    {
        try { return (new TextRange(box.Document.ContentStart, box.Document.ContentEnd).Text ?? "").TrimEnd('\r', '\n'); }
        catch { return ""; }
    }

    /// <summary>موقعیت کرت به‌صورت تعداد کاراکتر از ابتدای متن (۰ = ابتدا).</summary>
    public static int GetCharIndex(RichTextBox box)
    {
        try
        {
            string full = GetPlainText(box);
            var caret = box.CaretPosition;
            if (caret.CompareTo(box.Document.ContentStart) <= 0) return 0;
            string before = (new TextRange(box.Document.ContentStart, caret).Text ?? "")
                .Replace("\r\n", "\n").Replace('\r', '\n');
            // حذف حداکثر یک ‎\n‎ انتهاییِ ساختاری (پایان پاراگراف جزو متن نیست)
            if (before.EndsWith('\n'))
            {
                string without = before[..^1];
                if (full.StartsWith(without, StringComparison.Ordinal))
                    return without.Length;
            }
            if (full.StartsWith(before, StringComparison.Ordinal))
                return before.Length;
            return Math.Min(before.Length, full.Length);
        }
        catch { return 0; }
    }

    public static void SetCharIndex(RichTextBox box, int index)
    {
        try
        {
            string full = GetPlainText(box);
            index = Math.Clamp(index, 0, full.Length);
            int remaining = index;
            foreach (var block in box.Document.Blocks)
            {
                if (block is not Paragraph p) continue;
                foreach (var inline in p.Inlines)
                {
                    if (inline is not Run run) continue;
                    string s = run.Text ?? "";
                    if (remaining <= s.Length)
                    {
                        box.CaretPosition = run.ContentStart.GetPositionAtOffset(remaining, LogicalDirection.Forward)
                            ?? run.ContentEnd;
                        return;
                    }
                    remaining -= s.Length;
                }
            }
            box.CaretPosition = box.Document.ContentEnd;
        }
        catch { try { box.CaretPosition = box.Document.ContentEnd; } catch { } }
    }
}
