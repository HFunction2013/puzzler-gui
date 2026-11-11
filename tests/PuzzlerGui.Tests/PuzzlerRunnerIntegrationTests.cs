using PuzzlerGui.Models;
using PuzzlerGui.Services;

namespace PuzzlerGui.Tests;

/// <summary>
/// These tests need a real puzzler executable. They are skipped when the
/// PUZZLER_BIN environment variable does not point at one, so the normal
/// CI test run stays self-contained; the release smoke test sets it.
/// </summary>
public sealed class PuzzlerRunnerIntegrationTests
{
    private const string FourDominoes = """
        puzzle:
          name: "four dominoes"
          all_use: true
          can_rotate: true
          can_flip: false
          board_shape: rectangle{4,2}
          pieces:
            - id: "domino"
              shape: |
                ##
              count: 4
        """;

    private static string? Binary =>
        Environment.GetEnvironmentVariable("PUZZLER_BIN") is { Length: > 0 } path && File.Exists(path)
            ? path
            : null;

    [Fact]
    public async Task Solve_FourDominoes_ReturnsOneDistinctSolution()
    {
        if (Binary is null)
        {
            return;
        }

        var document = PuzzleYaml.Load(FourDominoes);
        var runner = new PuzzlerRunner(() => Binary);
        var outcome = await runner.SolveAsync(
            document,
            new SolveParameters { MaxSolutions = 1 },
            CancellationToken.None);

        Assert.Equal(SolveStatus.Success, outcome.Status);
        Assert.NotNull(outcome.Report);
        Assert.Single(outcome.Report!.Solutions);
        Assert.Equal(4, outcome.Report.Stats.SymmetryGroup);
        Assert.Equal(2, outcome.Report.Solutions[0].Grid.Length);
        Assert.Equal(4, outcome.Report.Solutions[0].Grid[0].Length);
    }

    [Fact]
    public async Task Solve_ImpossiblePuzzle_ReportsNoSolution()
    {
        if (Binary is null)
        {
            return;
        }

        var document = new PuzzleDocument("impossible", BoardModel.Rectangle(3, 3));
        document.Pieces.Add(new PieceModel("square", ShapeModel.FromText("##\n##"), 2));
        document.Pieces.Add(new PieceModel("single", ShapeModel.SingleCell(), 1));

        var runner = new PuzzlerRunner(() => Binary);
        var outcome = await runner.SolveAsync(
            document,
            new SolveParameters(),
            CancellationToken.None);

        Assert.Equal(SolveStatus.NoSolution, outcome.Status);
    }

    [Fact]
    public async Task Validate_FourDominoes_IsValid()
    {
        if (Binary is null)
        {
            return;
        }

        var document = PuzzleYaml.Load(FourDominoes);
        var runner = new PuzzlerRunner(() => Binary);
        var outcome = await runner.ValidateAsync(document, CancellationToken.None);

        Assert.True(outcome.IsValid, outcome.Output);
    }
}
