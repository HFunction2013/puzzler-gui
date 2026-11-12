using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PuzzlerGui.Services;

namespace PuzzlerGui.Views;

public sealed partial class SettingsWindow : Window, INotifyPropertyChanged
{
    private readonly string _originalLanguageCode;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public LocalizationService Loc { get; }

    public IReadOnlyDictionary<string, string> Strings => Loc.Strings;

    public SettingsWindow()
        : this(new AppSettings())
    {
    }

    public SettingsWindow(AppSettings settings)
    {
        Loc = LocalizationService.Instance;
        Loc.PropertyChanged += OnLocChanged;
        Closed += (_, _) => Loc.PropertyChanged -= OnLocChanged;
        _originalLanguageCode = Loc.CurrentLanguage;
        InitializeComponent();
        DataContext = this;

        PathBox.Text = settings.SolverPath ?? string.Empty;
        LanguageBox.ItemsSource = Loc.Languages;
        LanguageBox.SelectedItem = Loc.Languages.FirstOrDefault(option => option.Code == Loc.CurrentLanguage)
            ?? Loc.Languages[0];
        LanguageBox.SelectionChanged += OnLanguageChanged;
        UpdateStatus();
    }

    public string? SolverPath =>
        string.IsNullOrWhiteSpace(PathBox.Text) ? null : PathBox.Text!.Trim();

    public string SelectedLanguageCode =>
        (LanguageBox.SelectedItem as LanguageOption)?.Code ?? Loc.CurrentLanguage;

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(LocalizationService.Strings) and not "Item[]")
        {
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Loc)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Strings)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc["dialog.solverPickerTitle"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(Loc["file.executableFilter"])
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
            StatusText.Text = Loc["dialog.solverNotFound"];
            return;
        }

        PathBox.Text = found;
        UpdateStatus();
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is LanguageOption selected && selected.Code != Loc.CurrentLanguage)
        {
            Loc.SetLanguage(selected.Code);
            UpdateStatus();
        }
    }

    private void UpdateStatus()
    {
        var found = PuzzlerRunner.LocateSolver(SolverPath);
        StatusText.Text = found is null
            ? Loc["dialog.solverNotFound"]
            : string.Format(Loc["dialog.solverFound"], found);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Loc.SetLanguage(_originalLanguageCode);
        Close(false);
    }
}
