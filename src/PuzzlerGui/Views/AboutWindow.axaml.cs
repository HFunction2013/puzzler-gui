using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PuzzlerGui.Views;

public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null ? "Version 0.1.0" : $"Version {version.ToString(3)}";
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
