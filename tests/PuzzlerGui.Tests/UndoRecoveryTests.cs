using PuzzlerGui.Services;
using PuzzlerGui.ViewModels;

namespace PuzzlerGui.Tests;

public sealed class UndoRecoveryTests : IDisposable
{
    private readonly string _configHome;

    public UndoRecoveryTests()
    {
        _configHome = Path.Combine(Path.GetTempPath(), $"puzzler-gui-test-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configHome);
    }

    [Fact]
    public void Undo_Then_Redo_RestoresBoardSize()
    {
        var viewModel = NewViewModel();

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);

        viewModel.BoardWidthValue = 5;
        Assert.Equal(5, viewModel.Document.Board.Width);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);

        viewModel.UndoCommand.Execute(null);
        Assert.Equal(4, viewModel.Document.Board.Width);
        Assert.False(viewModel.CanUndo);
        Assert.True(viewModel.CanRedo);

        viewModel.RedoCommand.Execute(null);
        Assert.Equal(5, viewModel.Document.Board.Width);
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void Mutation_PersistsRecovery_AndDiscardRemovesIt()
    {
        var viewModel = NewViewModel();

        Assert.False(File.Exists(MainWindowViewModel.RecoveryFile));

        viewModel.BoardHeightValue = 5;
        Assert.True(File.Exists(MainWindowViewModel.RecoveryFile));

        viewModel.DiscardRecovery();
        Assert.False(File.Exists(MainWindowViewModel.RecoveryFile));
    }

    [Fact]
    public void TryLoadRecoveryText_RestoresSavedDocument()
    {
        var source = NewViewModel();
        source.BoardWidthValue = 6;
        var recoveryText = PuzzleYaml.Save(source.Document);

        var restored = NewViewModel();
        Assert.True(restored.TryLoadRecoveryText(recoveryText));
        Assert.Equal(6, restored.Document.Board.Width);
        Assert.False(File.Exists(MainWindowViewModel.RecoveryFile));
    }

    private static MainWindowViewModel NewViewModel() => new(new AppSettings());

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_configHome))
            {
                Directory.Delete(_configHome, recursive: true);
            }
        }
        catch (IOException)
        {
            // Test cleanup is best-effort.
        }
    }
}
