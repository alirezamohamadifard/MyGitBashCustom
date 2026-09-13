using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyGitBashCustom.Helpers;
using MyGitBashCustom.Services;
using MyGitBashCustom.ViewModels;

namespace MyGitBashCustom.Views;

/// <summary>نمای یک تب ترمینال: خروجی، اینتلیسنس، ورودی با رنگ‌بندی نحوی زنده.</summary>
public partial class TerminalView : UserControl
{
    private bool _syncBox;
    private string _lastHighlighted = "\0"; // نگهبان بازسازی تکراری

    private static readonly SolidColorBrush CCommand = B("#4FC3FF");
    private static readonly SolidColorBrush CSub = B("#A8D8FF");
    private static readonly SolidColorBrush CFlag = B("#F6C453");
    private static readonly SolidColorBrush CString = B("#69DB91");
    private static readonly SolidColorBrush CNum = B("#C7A7FF");
    private static readonly SolidColorBrush CPath = B("#52DDD8");
    private static readonly SolidColorBrush COp = B("#FF8585");
    private static readonly SolidColorBrush CComment = B("#7F8DA3");
    private static readonly SolidColorBrush CVar = B("#FFB86B");
    private static readonly SolidColorBrush CPlain = B("#EAF1F8");
    private static readonly SolidColorBrush CErr = B("#FF6B6B");
    private static SolidColorBrush B(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private TerminalViewModel? Vm => DataContext as TerminalViewModel;

    public TerminalView()
    {
        InitializeComponent();

        InputBox.TextChanged += (_, __) =>
        {
            if (_syncBox) return;
            var plain = RichTextCaret.GetPlainText(InputBox);
            // تمیزکاری Paste چندخطی: خط‌ها با ; به هم می‌چسبند تا یک دستور معتبر شود
            if (plain.Contains('\n') || plain.Contains('\r'))
            {
                plain = NormalizeMultiline(plain);
                SetBoxText(plain);
                _lastHighlighted = plain;
            }
            else ApplyHighlight(); // همگام: کرت با ایندکس کاراکتری دقیق سر جایش می‌ماند
            if (Vm is not null && Vm.InputText != plain)
                Vm.InputText = plain;
        };
        InputBox.PreviewKeyDown += InputBox_KeyDown;
        DataContextChanged += OnDcChanged;
        Loaded += (_, __) => { ApplyFontSettings(); HookVm(Vm); InputBox.Focus(); };
        Unloaded += (_, __) => UnhookVm();
        SuggestList.MouseDoubleClick += (_, __) => Vm?.AcceptSuggestionCommand.Execute(null);
        App.Settings.Changed += OnSettingsChanged;
    }

    private TerminalViewModel? _hooked;
    private void OnDcChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnhookVm();
        HookVm(Vm);
        SetBoxText(Vm?.InputText ?? "");
    }

    private void HookVm(TerminalViewModel? vm)
    {
        if (vm is null || _hooked == vm) return;
        UnhookVm();
        _hooked = vm;
        vm.PropertyChanged += Vm_PropChanged;
        vm.InputHighlightRequested += OnHighlightRequested;
        if (vm.Lines is INotifyCollectionChanged cc)
            cc.CollectionChanged += Lines_Changed;
    }

    private void UnhookVm()
    {
        if (_hooked is null) return;
        _hooked.PropertyChanged -= Vm_PropChanged;
        _hooked.InputHighlightRequested -= OnHighlightRequested;
        if (_hooked.Lines is INotifyCollectionChanged cc)
            cc.CollectionChanged -= Lines_Changed;
        _hooked = null;
    }

    private void OnSettingsChanged() => Dispatcher.BeginInvoke(ApplyFontSettings);

    private void OnHighlightRequested() => SetBoxText(Vm?.InputText ?? "");

    private void Vm_PropChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalViewModel.InputText) && Vm is not null)
        {
            var v = Vm.InputText ?? "";
            if (GetBoxText() != v) SetBoxText(v);
        }
    }

    private void Lines_Changed(object? s, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add)
            Dispatcher.BeginInvoke(() =>
            {
                if (OutputList.Items.Count > 0)
                    OutputList.ScrollIntoView(OutputList.Items[^1]);
            }, DispatcherPriority.Background);
    }

    // ── رنگ‌بندی نحوی ──
    private static string NormalizeMultiline(string s)
    {
        var parts = s.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(p => p.Trim()).Where(p => p.Length > 0);
        return string.Join(" ; ", parts);
    }

    private string GetBoxText() => RichTextCaret.GetPlainText(InputBox);

    private void SetBoxText(string text)
    {
        _syncBox = true;
        try
        {
            text ??= "";
            InputBox.Document.Blocks.Clear();
            var p = new Paragraph { Margin = new Thickness(0) };
            AppendColored(p, text);
            InputBox.Document.Blocks.Add(p);
            InputBox.CaretPosition = InputBox.Document.ContentEnd;
            _lastHighlighted = text;
        }
        finally { _syncBox = false; }
    }

    private void ApplyHighlight()
    {
        if (_syncBox) return;
        _syncBox = true;
        try
        {
            int index = RichTextCaret.GetCharIndex(InputBox);
            string plain = RichTextCaret.GetPlainText(InputBox);
            if (plain == _lastHighlighted) return; // چیزی عوض نشده
            InputBox.Document.Blocks.Clear();
            var p = new Paragraph { Margin = new Thickness(0) };
            if (App.Settings.Current.EnableSyntaxHighlight)
                AppendColored(p, plain);
            else
                p.Inlines.Add(new Run(plain) { Foreground = CPlain });
            InputBox.Document.Blocks.Add(p);
            _lastHighlighted = plain;
            RichTextCaret.SetCharIndex(InputBox, index);
        }
        catch (Exception ex) { AppLogger.Instance.Warn("Highlight", "خطا در رنگ‌بندی", ex); }
        finally { _syncBox = false; }
    }

    private static void AppendColored(Paragraph p, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var tokens = BashSyntaxTokenizer.Tokenize(text);
        int cursor = 0;
        foreach (var tok in tokens)
        {
            if (tok.Start > cursor)
                p.Inlines.Add(new Run(text[cursor..tok.Start]) { Foreground = CPlain });
            var run = new Run(tok.Text);
            switch (tok.Kind)
            {
                case TokenKind.Command: run.Foreground = CCommand; run.FontWeight = FontWeights.Bold; break;
                case TokenKind.SubCommand: run.Foreground = CSub; run.FontWeight = FontWeights.Bold; break;
                case TokenKind.Flag: run.Foreground = CFlag; break;
                case TokenKind.String: run.Foreground = CString; break;
                case TokenKind.Number: run.Foreground = CNum; break;
                case TokenKind.Path: run.Foreground = CPath; run.TextDecorations = TextDecorations.Underline; break;
                case TokenKind.Operator: run.Foreground = COp; run.FontWeight = FontWeights.Bold; break;
                case TokenKind.Comment: run.Foreground = CComment; run.FontStyle = FontStyles.Italic; break;
                case TokenKind.Variable: run.Foreground = CVar; run.FontWeight = FontWeights.Bold; break;
                case TokenKind.Error: run.Foreground = CErr; run.TextDecorations = TextDecorations.Underline; break;
                default: run.Foreground = CPlain; break;
            }
            p.Inlines.Add(run);
            cursor = tok.Start + tok.Length;
        }
        if (cursor < text.Length)
            p.Inlines.Add(new Run(text[cursor..]) { Foreground = CPlain });
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        var t = Vm;
        if (t is null) return;
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        // قرارداد بدون ابهام ترمینال:
        // هنگام اجرای فرایند Ctrl+C همیشه SIGINT می‌فرستد؛ کپی همیشه Ctrl+Shift+C است.
        // این ترتیب جلوی کپی‌شدن تصادفی به‌جای خاموش‌شدن سرورهای API را می‌گیرد.
        if (ctrl && e.Key == Key.C && !shift)
        {
            if (t.IsBusy)
            {
                e.Handled = true;
                t.StopCommand.Execute(null);
                return;
            }
            // وقتی فرایندی اجرا نمی‌شود، Ctrl+C رفتار معمول ویرایشگر را دارد.
            return;
        }
        // Ctrl+L مثل Bash: پاک‌کردن صفحه
        if (ctrl && e.Key == Key.L && !shift)
        {
            e.Handled = true;
            t.ClearCommand.Execute(null);
            return;
        }
        // Ctrl+U مثل Bash: پاک‌کردن خط ورودی
        if (ctrl && e.Key == Key.U && !shift)
        {
            e.Handled = true;
            t.InputText = "";
            SetBoxText("");
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            _ = t.ExecuteAsync();
        }
        else if (e.Key == Key.Tab)
        {
            e.Handled = true;
            t.AcceptSuggestionCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            t.IsSuggestOpen = false;
        }
        else if (e.Key == Key.Up)
        {
            e.Handled = true;
            if (t.IsSuggestOpen && t.Suggestions.Count > 0)
                t.SuggestIndex = (t.SuggestIndex - 1 + t.Suggestions.Count) % t.Suggestions.Count;
            else t.HistoryPrevCommand.Execute(null);
        }
        else if (e.Key == Key.Down)
        {
            e.Handled = true;
            if (t.IsSuggestOpen && t.Suggestions.Count > 0)
                t.SuggestIndex = (t.SuggestIndex + 1) % t.Suggestions.Count;
            else t.HistoryNextCommand.Execute(null);
        }
    }

    private void ApplyFontSettings()
    {
        try
        {
            var s = App.Settings.Current;
            InputBox.FontFamily = new FontFamily("Consolas, Cascadia Code," + s.FontFamily);
            InputBox.FontSize = s.FontSize;
            OutputList.FontSize = s.FontSize;
        }
        catch { }
    }
}
