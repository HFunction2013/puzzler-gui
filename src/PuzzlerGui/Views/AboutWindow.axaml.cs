using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PuzzlerGui.Services;

namespace PuzzlerGui.Views;

public sealed partial class AboutWindow : Window
{
    public LocalizationService Loc { get; }

    public AboutWindow()
    {
        Loc = LocalizationService.Instance;
        InitializeComponent();
        DataContext = this;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? Loc["about.version"]
            : string.Format(Loc["about.version"], version.ToString(3));
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
