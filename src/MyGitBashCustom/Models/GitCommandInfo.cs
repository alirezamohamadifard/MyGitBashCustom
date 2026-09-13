namespace MyGitBashCustom.Models;

public enum CommandCategory { Git, Bash, File, Network, System, Custom }

public sealed record GitCommandInfo(
    string Name,
    string Syntax,
    string DescriptionFa,
    string DescriptionEn,
    CommandCategory Category,
    string Example,
    string[] AliasesValues)
{
    public string[] SearchTokens { get; init; } = [];
}
