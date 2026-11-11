namespace PuzzlerGui.Models;

/// <summary>The parameters passed through to the command line solver.</summary>
public sealed class SolveParameters
{
    public int? MaxSolutions { get; set; }

    public long? NodeLimit { get; set; }

    public double? TimeLimitSeconds { get; set; }

    public bool Dedupe { get; set; } = true;

    public ulong? Seed { get; set; }
}

public enum SolveStatus
{
    Success,
    NoSolution,
    InvalidPuzzle,
    SolverMissing,
    Cancelled,
    Failed,
}

public sealed class SolveOutcome
{
    public SolveStatus Status { get; init; }

    public SolveReport? Report { get; init; }

    public string Message { get; init; } = string.Empty;

    public string RawJson { get; init; } = string.Empty;

    public TimeSpan Duration { get; init; }
}

public sealed class ValidateOutcome
{
    public bool IsValid { get; init; }

    public string Output { get; init; } = string.Empty;
}
