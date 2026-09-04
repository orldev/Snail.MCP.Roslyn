namespace Snail.MCP.Roslyn.Tools.Prompts;

/// <summary>Text for the parameters most tools share; a wire contract, edited with caution.</summary>
/// <remarks>Every byte here is sent once per tool that uses it, in every request the client makes: the solution
/// id alone appeared twenty-nine times. What the whole family has in common — that a symbol is addressed by id,
/// by name or by position — is said once in the server instructions instead of once per parameter.</remarks>
public static class ParameterDescriptions
{
    public const string Solution = "From load_solution.";

    public const string SymbolId = "Symbol id from search_symbols.";

    public const string SymbolName = "Bare name; used only when it matches exactly one symbol.";

    public const string TypeName = "Bare type name; used only when it matches exactly one type.";

    public const string File = "Absolute file path.";

    public const string Line = "1-based line.";

    public const string Column = "1-based column.";
}
