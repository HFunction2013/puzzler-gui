using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using PuzzlerGui.Services;
using PuzzlerGui.ViewModels;
using PuzzlerGui.Views;

namespace PuzzlerGui;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsStore.Load();
            ApplyTheme(settings.Theme);

            var viewModel = new MainWindowViewModel(settings);
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var initialFile = GetInitialFile(desktop.Args);
            if (initialFile is not null)
            {
                window.Opened += (_, _) => window.OpenFile(initialFile);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string? GetInitialFile(string[]? args)
    {
        if (args is null)
        {
            return null;
        }

        foreach (var argument in args)
        {
            if (argument.StartsWith('-'))
            {
                continue;
            }

            if (File.Exists(argument) &&
                argument.EndsWith(".pzle", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(argument);
            }
        }

        return null;
    }

    private void ApplyTheme(string theme)
    {
        RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
