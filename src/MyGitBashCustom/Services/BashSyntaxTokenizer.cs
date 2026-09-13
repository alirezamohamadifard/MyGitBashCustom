namespace MyGitBashCustom.Services;

public enum TokenKind { Command, SubCommand, Flag, String, Number, Path, Operator, Comment, Variable, Plain, Error }
public sealed record SyntaxToken(TokenKind Kind, string Text, int Start, int Length);

/// <summary>توکنایزر سبک و state-aware برای Bash/Git با پشتیبانی از quote، escape، substitution و pipeline.</summary>
public static class BashSyntaxTokenizer
{
    private static readonly HashSet<string> GitSubs = new(StringComparer.OrdinalIgnoreCase)
    { "status","add","commit","push","pull","fetch","branch","checkout","switch","merge","rebase","log","diff","stash","reset","revert","clone","init","remote","tag","show","blame","clean","restore","config","cherry-pick","worktree","bisect","grep","describe","reflog","submodule" };
    private static readonly HashSet<string> CommandPrefixes = new(StringComparer.OrdinalIgnoreCase)
    { "sudo", "env", "command", "builtin", "time", "nohup" };

    public static List<SyntaxToken> Tokenize(string line)
    {
        var result = new List<SyntaxToken>();
        if (string.IsNullOrEmpty(line)) return result;
        int i = 0; bool expectCommand = true, expectGitSubcommand = false;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }
            int start = i; char c = line[i];
            if (c == '#') { result.Add(new(TokenKind.Comment, line[i..], i, line.Length - i)); break; }
            if (c is '\'' or '"' or '`')
            {
                char quote = c; i++; bool closed = false;
                while (i < line.Length)
                {
                    if (line[i] == '\\' && quote != '\'' && i + 1 < line.Length) { i += 2; continue; }
                    if (line[i++] == quote) { closed = true; break; }
                }
                result.Add(new(closed ? TokenKind.String : TokenKind.Error, line[start..i], start, i - start));
                expectCommand = false; continue;
            }
            if (c == '$')
            {
                i++;
                if (i < line.Length && line[i] is '{' or '(')
                {
                    char open = line[i++], close = open == '{' ? '}' : ')'; int depth = 1;
                    while (i < line.Length && depth > 0)
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                        if (line[i] == open) depth++; else if (line[i] == close) depth--; i++;
                    }
                }
                else while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '_' or '?' or '!' or '#')) i++;
                result.Add(new(TokenKind.Variable, line[start..i], start, i - start)); expectCommand = false; continue;
            }
            if (IsOperatorStart(c))
            {
                i++;
                if (i < line.Length && ((line[i] == c && c is '|' or '&' or '>' or '<') || (c is '>' or '<' && line[i] == '&'))) i++;
                result.Add(new(TokenKind.Operator, line[start..i], start, i - start));
                if (c is '|' or ';' or '&') { expectCommand = true; expectGitSubcommand = false; }
                continue;
            }
            while (i < line.Length && !char.IsWhiteSpace(line[i]) && !IsOperatorStart(line[i]) && line[i] is not ('#' or '\'' or '"' or '`' or '$'))
            { if (line[i] == '\\' && i + 1 < line.Length) i += 2; else i++; }
            string word = line[start..i]; TokenKind kind;
            if (word.StartsWith('-') && word.Length > 1) kind = TokenKind.Flag;
            else if (IsAssignment(word)) kind = TokenKind.Variable;
            else if (IsNumber(word)) kind = TokenKind.Number;
            else if (expectGitSubcommand) { kind = GitSubs.Contains(word) ? TokenKind.SubCommand : TokenKind.Plain; expectGitSubcommand = false; }
            else if (expectCommand)
            {
                // فرمان‌های سفارشی معتبرند؛ ناشناخته‌بودن نباید به‌اشتباه خطا نمایش داده شود.
                kind = TokenKind.Command; expectCommand = CommandPrefixes.Contains(word);
                if (word.Equals("git", StringComparison.OrdinalIgnoreCase)) { expectGitSubcommand = true; expectCommand = false; }
            }
            else if (LooksLikePath(word)) kind = TokenKind.Path;
            else kind = TokenKind.Plain;
            result.Add(new(kind, word, start, i - start));
        }
        return result;
    }

    private static bool IsOperatorStart(char c) => c is '|' or ';' or '>' or '<' or '&';
    private static bool IsAssignment(string s)
    {
        int eq = s.IndexOf('='); if (eq < 1) return false;
        string name = s[..eq].TrimEnd('+');
        return name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_') && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }
    private static bool IsNumber(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
    private static bool LooksLikePath(string s) => s is "." or ".." or "~" || s.StartsWith("./") || s.StartsWith("../") || s.StartsWith("~/") || s.Contains('/') || s.Contains('\\') || (s.Length > 2 && s[1] == ':');
}
