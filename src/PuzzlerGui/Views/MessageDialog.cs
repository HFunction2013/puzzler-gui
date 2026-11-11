using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PuzzlerGui.Views;

/// <summary>A small modal dialog with up to three buttons.</summary>
public sealed class MessageDialog : Window
{
    public MessageDialog(
        string title,
        string message,
        string primaryButton,
        string? secondaryButton = null,
        string? tertiaryButton = null)
    {
        Title = title;
        Width = 500;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 18,
        };
        content.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        if (tertiaryButton is not null)
        {
            buttons.Children.Add(CreateButton(tertiaryButton));
        }

        if (secondaryButton is not null)
        {
            buttons.Children.Add(CreateButton(secondaryButton));
        }

        buttons.Children.Add(CreateButton(primaryButton));

        content.Children.Add(buttons);
        Content = content;
    }

    public Task<string> ShowAsync(Window owner) => ShowDialog<string>(owner);

    private Button CreateButton(string label)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 88,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        button.Click += (_, _) => Close(label);
        return button;
    }
}
