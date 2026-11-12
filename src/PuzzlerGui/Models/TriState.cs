namespace PuzzlerGui.Models;

public enum TriState
{
    Inherit,
    Allow,
    Forbid,
}

public sealed record TriStateOption(TriState Value, string Display);

public static class TriStateExtensions
{
    public static bool? ToNullable(this TriState value) => value switch
    {
        TriState.Allow => true,
        TriState.Forbid => false,
        _ => null,
    };

    public static TriState FromNullable(bool? value) => value switch
    {
        true => TriState.Allow,
        false => TriState.Forbid,
        _ => TriState.Inherit,
    };
}
