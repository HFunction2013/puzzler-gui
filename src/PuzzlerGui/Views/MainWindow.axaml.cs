using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.VisualTree;
using PuzzlerGui.Controls;
using PuzzlerGui.Services;
using PuzzlerGui.ViewModels;

namespace PuzzlerGui.Views;

public sealed partial class MainWindow : Window
{
    private bool _forceClose;
    private bool _closePromptOpen;

    public MainWindow()
    {
        InitializeComponent();

        var boardCanvas = this.FindControl<GridCanvas>("BoardCanvas");
        if (boardCanvas is not null)
        {
            boardCanvas.CellPaintRequested += (_, args) =>
                ViewModel.PaintBoardCell(args.Row, args.Column, args.Filled);
        }

        var pieceCanvas = this.FindControl<GridCanvas>("PieceCanvas");
        if (pieceCanvas is not null)
        {
            pieceCanvas.CellPaintRequested += (_, args) =>
                ViewModel.PaintPieceCell(args.Row, args.Column, args.Filled);
        }

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public void OpenFile(string path) => _ = OpenPathAsync(path, confirmDiscard: false);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var control = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (control && e.Key == Key.N)
        {
            e.Handled = true;
            _ = NewDocumentAsync();
        }
        else if (control && e.Key == Key.O)
        {
            e.Handled = true;
            _ = OpenWithDialogAsync();
        }
        else if (control && e.Key == Key.S)
        {
            e.Handled = true;
            _ = SaveAsync(forceDialog: shift);
        }
        else if (e.Key == Key.F5)
        {
            e.Handled = true;
            if (ViewModel.SolveCommand.CanExecute(null))
            {
                ViewModel.SolveCommand.Execute(null);
            }
        }
        else if (e.Key == Key.Escape && ViewModel.IsSolving)
        {
            e.Handled = true;
            ViewModel.CancelSolveCommand.Execute(null);
        }
        else if (ViewModel.SelectedTabIndex == 1 &&
                 !e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                 !e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
                 !e.KeyModifiers.HasFlag(KeyModifiers.Shift) &&
                 !IsEditingFocusElement(e.Source))
        {
            switch (e.Key)
            {
                case Key.Left:
                    if (ViewModel.PreviousSolutionCommand.CanExecute(null))
                    {
                        ViewModel.PreviousSolutionCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Right:
                    if (ViewModel.NextSolutionCommand.CanExecute(null))
                    {
                        ViewModel.NextSolutionCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Home:
                    if (ViewModel.FirstSolutionCommand.CanExecute(null))
                    {
                        ViewModel.FirstSolutionCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.End:
                    if (ViewModel.LastSolutionCommand.CanExecute(null))
                    {
                        ViewModel.LastSolutionCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
            }
        }
    }

    private static bool IsEditingFocusElement(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        Visual? current = visual;
        while (current is not null)
        {
            if (current is TextBox or NumericUpDown or ComboBox or ListBox or Menu or MenuItem)
            {
                return true;
            }

            current = current.GetVisualParent();
        }

        return false;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_forceClose)
        {
            base.OnClosing(e);
            return;
        }

        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel is null)
        {
            base.OnClosing(e);
            return;
        }

        if (viewModel.IsSolving)
        {
            viewModel.CancelSolveCommand.Execute(null);
        }

        if (!viewModel.IsDirty)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        _ = ConfirmCloseAsync();
    }

    private async Task ConfirmCloseAsync()
    {
        if (_closePromptOpen)
        {
            return;
        }

        _closePromptOpen = true;
        try
        {
            if (await ConfirmDiscardAsync())
            {
                _forceClose = true;
                Close();
            }
        }
        finally
        {
            _closePromptOpen = false;
        }
    }

    private async Task<bool> ConfirmDiscardAsync()
    {
        var viewModel = ViewModel;
        if (!viewModel.IsDirty)
        {
            return true;
        }

        var result = await new MessageDialog(
            "Unsaved changes",
            $"\u201c{viewModel.CurrentFileName}\u201d has unsaved changes. Save them before continuing?",
            "Save",
            "Discard",
            "Cancel").ShowAsync(this);

        return result switch
        {
            "Save" => await SaveAsync(forceDialog: false),
            "Discard" => true,
            _ => false,
        };
    }

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        ViewModel.NewDocument();
    }

    private async Task OpenWithDialogAsync()
    {
        if (!await ConfirmDiscardAsync())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open puzzle",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Puzzle files (*.pzle)")
                {
                    Patterns = new[] { "*.pzle" },
                },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (path is null)
        {
            await ShowError("Cannot open file", "The selected file has no local path.");
            return;
        }

        await OpenPathAsync(path, confirmDiscard: false);
    }

    private async Task<bool> OpenPathAsync(string path, bool confirmDiscard)
    {
        if (confirmDiscard && !await ConfirmDiscardAsync())
        {
            return false;
        }

        if (!ViewModel.LoadFile(path, out var error))
        {
            await ShowError("Cannot open puzzle", error ?? "Unknown error.");
            return false;
        }

        return true;
    }

    private async Task<bool> SaveAsync(bool forceDialog)
    {
        var viewModel = ViewModel;
        var path = forceDialog ? null : viewModel.CurrentFilePath;
        if (path is null)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save puzzle",
                SuggestedFileName = viewModel.CurrentFilePath is null
                    ? "puzzle.pzle"
                    : Path.GetFileName(viewModel.CurrentFilePath),
                DefaultExtension = "pzle",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Puzzle files (*.pzle)")
                    {
                        Patterns = new[] { "*.pzle" },
                    },
                },
            });

            if (file is null)
            {
                return false;
            }

            path = file.TryGetLocalPath() ?? file.Path.LocalPath;
        }

        if (!viewModel.SaveTo(path, out var error))
        {
            await ShowError("Cannot save puzzle", error ?? "Unknown error.");
            return false;
        }

        return true;
    }

    private async Task ShowError(string title, string message) =>
        await new MessageDialog(title, message, "OK").ShowAsync(this);

    // ------------------------------------------------------------------ menu

    private async void OnNewClick(object? sender, RoutedEventArgs e) => await NewDocumentAsync();

    private async void OnOpenClick(object? sender, RoutedEventArgs e) => await OpenWithDialogAsync();

    private async void OnSaveClick(object? sender, RoutedEventArgs e) => await SaveAsync(false);

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveAsync(true);

    private async void OnSampleClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string fileName })
        {
            return;
        }

        var path = Path.Combine(ViewModel.SamplesDirectory, fileName);
        if (!File.Exists(path))
        {
            await ShowError("Sample not found", $"Expected the sample at:\n{path}");
            return;
        }

        await OpenPathAsync(path, confirmDiscard: true);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnSolveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.SolveCommand.CanExecute(null))
        {
            ViewModel.SolveCommand.Execute(null);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) =>
        ViewModel.CancelSolveCommand.Execute(null);

    private void OnValidateClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.ValidateWithSolverCommand.CanExecute(null))
        {
            ViewModel.ValidateWithSolverCommand.Execute(null);
        }
    }

    private async void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(ViewModel.Settings.SolverPath);
        var accepted = await dialog.ShowDialog<bool>(this);
        if (accepted)
        {
            ViewModel.SetSolverPath(dialog.SolverPath);
            ViewModel.SetBanner(
                BannerKind.Info,
                ViewModel.SolverFound
                    ? "Solver path updated."
                    : "The selected solver path does not exist.");
        }
    }

    private async void OnAboutClick(object? sender, RoutedEventArgs e) =>
        await new AboutWindow().ShowDialog(this);

    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string theme })
        {
            return;
        }

        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        ViewModel.Settings.Theme = theme;
        SettingsStore.Save(ViewModel.Settings);
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        var name = ViewModel.Document.Name;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "puzzle";
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export solutions",
            SuggestedFileName = $"{name}-solutions.json",
            DefaultExtension = "json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON files (*.json)")
                {
                    Patterns = new[] { "*.json" },
                },
            },
        });

        if (file is null)
        {
            return;
        }

        var path = file.TryGetLocalPath() ?? file.Path.LocalPath;
        if (!ViewModel.SaveReportJson(path, out var error))
        {
            await ShowError("Cannot export report", error ?? "Unknown error.");
        }
    }

    // ------------------------------------------------------------ drag & drop

    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // The DataTransfer API is not available on all target frameworks yet.
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
#pragma warning restore CS0618
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618
        var files = e.Data.GetFiles();
#pragma warning restore CS0618
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is null || !path.EndsWith(".pzle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await OpenPathAsync(path, confirmDiscard: true);
            return;
        }

        await ShowError("Unsupported file", "Drop a .pzle puzzle file to open it.");
    }
}
