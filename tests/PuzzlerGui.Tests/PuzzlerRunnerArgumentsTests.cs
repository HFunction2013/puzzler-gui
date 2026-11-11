using PuzzlerGui.Models;
using PuzzlerGui.Services;

namespace PuzzlerGui.Tests;

public sealed class PuzzlerRunnerArgumentsTests
{
    [Fact]
    public void DefaultParametersEnableCompressedJson()
    {
        Assert.True(new SolveParameters().CompressJson);
    }

    [Fact]
    public void BuildArguments_AddsCompressByDefault()
    {
        var arguments = PuzzlerRunner.BuildArguments(new SolveParameters(), "in.pzle", "out.json");

        Assert.Contains("--compress", arguments);
        Assert.DoesNotContain("--no-dedupe", arguments);
    }

    [Fact]
    public void BuildArguments_OmitsCompressWhenDisabledAndPassesOtherFlags()
    {
        var parameters = new SolveParameters
        {
            CompressJson = false,
            Dedupe = false,
            MaxSolutions = 10,
            Seed = 7,
        };

        var arguments = PuzzlerRunner.BuildArguments(parameters, "in.pzle", "out.json");

        Assert.DoesNotContain("--compress", arguments);
        Assert.Contains("--no-dedupe", arguments);
        Assert.Contains("--limit", arguments);
        Assert.Contains("10", arguments);
        Assert.Contains("--seed", arguments);
        Assert.Contains("7", arguments);
    }
}
