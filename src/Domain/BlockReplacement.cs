namespace Snail.MCP.Roslyn.Domain;

/// <summary>One compact edit: delete lines StartLine through EndLine of File and put Text in their place.</summary>
/// <remarks>Anything derivable from the file the caller already has does not travel back to it: a change of a few
/// lines is stated as those lines, never as a copy of the file around them.</remarks>
internal sealed record BlockReplacement(
    string File,
    int StartLine,
    int EndLine,
    string Text);
