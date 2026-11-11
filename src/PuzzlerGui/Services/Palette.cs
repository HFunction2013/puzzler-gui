using Avalonia.Media;

namespace PuzzlerGui.Services;

/// <summary>
/// Golden-angle colour palette, mirroring the colours used by the command
/// line solver so that the GUI and the terminal output look consistent.
/// </summary>
public static class Palette
{
    private const int ColorCount = 24;

    private static readonly Color[] Colors = BuildPalette();

    /// <summary>Colour used for playable cells in the board editor.</summary>
    public static readonly Color BoardCell = Color.FromRgb(0x5F, 0x74, 0x92);

    public static IReadOnlyList<Color> All => Colors;

    public static Color ColorFor(int oneBasedIndex) =>
        Colors[(Math.Max(oneBasedIndex, 1) - 1) % Colors.Length];

    public static IReadOnlyList<Color> Single(Color color) => new[] { color };

    public static Color TextColorFor(Color background)
    {
        // Rec. 709 luma; dark text on light tiles, light text on dark tiles.
        var luma = 0.2126 * background.R + 0.7152 * background.G + 0.0722 * background.B;
        return luma > 140 ? Color.FromRgb(0x18, 0x1C, 0x24) : Color.FromRgb(0xF6, 0xF8, 0xFC);
    }

    private static Color[] BuildPalette()
    {
        var colors = new Color[ColorCount];
        for (var i = 0; i < ColorCount; i++)
        {
            var hue = i * 137.508 % 360.0;
            colors[i] = FromHsv(hue, 0.52, 0.88);
        }

        return colors;
    }

    private static Color FromHsv(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = value - c;
        var (r, g, b) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
