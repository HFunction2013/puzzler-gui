using PuzzlerGui.Models;
using PuzzlerGui.ViewModels;

namespace PuzzlerGui.Tests;

public sealed class ReportJsonExportTests
{
    [Fact]
    public void RawSolverJsonIsPreservedOnExport()
    {
        const string raw = "{\"generator\":\"puzzler\",\"solutions\":[]}";

        var exported = MainWindowViewModel.RenderReportJson(new SolveReport(), raw, compressJson: false);

        Assert.Equal(raw, exported);
    }

    [Fact]
    public void FallbackSerializationHonoursCompressFlag()
    {
        var compact = MainWindowViewModel.RenderReportJson(new SolveReport(), string.Empty, compressJson: true);
        var pretty = MainWindowViewModel.RenderReportJson(new SolveReport(), string.Empty, compressJson: false);

        Assert.DoesNotContain("\n", compact.TrimEnd());
        Assert.Contains("\n", pretty);
    }
}
