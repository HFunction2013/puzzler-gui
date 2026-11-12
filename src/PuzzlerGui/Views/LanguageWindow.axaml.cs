using Avalonia.Controls;
using Avalonia.Interactivity;
using PuzzlerGui.Services;

namespace PuzzlerGui.Views;

public sealed partial class LanguageWindow : Window
{
    public LocalizationService Loc { get; }

    public LanguageWindow()
    {
        Loc = LocalizationService.Instance;
        InitializeComponent();
        DataContext = this;

        LanguageBox.ItemsSource = Loc.Languages;
        LanguageBox.SelectedItem = Loc.Languages.FirstOrDefault(language => language.Code == Loc.CurrentLanguage)
            ?? Loc.Languages[0];
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (LanguageBox.SelectedItem is LanguageOption selected)
        {
            Loc.SetLanguage(selected.Code);
        }

        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
