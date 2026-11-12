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
            LocalizationService.Instance.Apply(settings.Language);
            ApplyTheme(settings.Theme);

            var viewModel = new MainWindowViewModel(settings);
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var initialFile = GetInitialFile(desktop.Args);
            window.Opened += async (_, _) =>
            {
                if (string.IsNullOrEmpty(settings.Language))
                {
                    var languageDialog = new LanguageWindow();
                    var accepted = await languageDialog.ShowDialog<bool?>(window);
                    if (accepted == true)
                    {
                        settings.Language = LocalizationService.Instance.CurrentLanguage;
                        SettingsStore.Save(settings);
                    }
                }

                if (initialFile is null && MainWindowViewModel.RecoveryFileExists)
                {
                    await window.AskRestoreRecoveryAsync();
                }

                if (initialFile is not null)
                {
                    window.OpenFile(initialFile);
                }
            };
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
