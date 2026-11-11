using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PuzzlerGui.Services;

namespace PuzzlerGui.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
        : this(null)
    {
    }

    public SettingsWindow(string? solverPath)
    {
        InitializeComponent();
        PathBox.Text = solverPath ?? string.Empty;
        UpdateStatus();
    }

    public string? SolverPath =>
        string.IsNullOrWhiteSpace(PathBox.Text) ? null : PathBox.Text!.Trim();

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select the puzzler executable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Executables")
                {
                    Patterns = OperatingSystem.IsWindows()
                        ? new[] { "*.exe" }
                        : new[] { "*" },
                },
            },
        });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            PathBox.Text = path;
            UpdateStatus();
        }
    }

    private void OnDetectClick(object? sender, RoutedEventArgs e)
    {
        var found = PuzzlerRunner.LocateSolver(null);
        if (found is null)
        {
            PathBox.Text = string.Empty;
            StatusText.Text = "No puzzler executable was found next to the app or on PATH.";
            return;
        }

        PathBox.Text = found;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var found = PuzzlerRunner.LocateSolver(SolverPath);
        StatusText.Text = found is null
            ? "Not found — put puzzler next to puzzler-gui, or browse to the binary."
            : $"Found: {found}";
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
