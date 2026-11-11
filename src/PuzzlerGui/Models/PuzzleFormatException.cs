namespace PuzzlerGui.Models;

/// <summary>Raised when a <c>.pzle</c> document is malformed.</summary>
public sealed class PuzzleFormatException : Exception
{
    public PuzzleFormatException(string message)
        : base(message)
    {
    }

    public PuzzleFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
