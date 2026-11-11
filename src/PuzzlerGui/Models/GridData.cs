using Avalonia.Media;

namespace PuzzlerGui.Models;

/// <summary>
/// An immutable snapshot of a cell grid, ready to be drawn by the canvas
/// controls. <see cref="Cells"/> uses 0 for a blocked cell and a 1-based
/// colour index for a playable cell.
/// </summary>
public sealed class GridData
{
    public GridData(int cols, int rows, int[] cells, string[] labels, IReadOnlyList<Color> palette)
    {
        Cols = cols;
        Rows = rows;
        Cells = cells;
        Labels = labels;
        Palette = palette;
    }

    public int Cols { get; }

    public int Rows { get; }

    public int[] Cells { get; }

    public string[] Labels { get; }

    public IReadOnlyList<Color> Palette { get; }

    public Color ColorAt(int index) =>
        Palette.Count == 0 ? Colors.Transparent : Palette[(Math.Max(index, 1) - 1) % Palette.Count];
}
