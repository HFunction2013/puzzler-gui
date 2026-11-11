using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using PuzzlerGui.Services;

namespace PuzzlerGui.Models;

/// <summary>
/// A polyomino footprint on a rectangular grid. Shapes kept by the editor are
/// normalised (no empty border rows or columns) whenever they come from a file.
/// </summary>
public sealed class ShapeModel : ObservableObject
{
    private int _width;
    private int _height;
    private bool[] _cells;

    public ShapeModel(int width, int height, bool[] cells)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "A shape needs at least one row and one column.");
        }

        if (cells.Length != width * height)
        {
            throw new ArgumentException("The cell array does not match the requested size.", nameof(cells));
        }

        _width = width;
        _height = height;
        _cells = cells;
    }

    public event EventHandler? Changed;

    public int Width => _width;

    public int Height => _height;

    public int Area => _cells.Count(c => c);

    public bool IsFilled(int row, int col) =>
        row >= 0 && row < _height && col >= 0 && col < _width && _cells[row * _width + col];

    public IReadOnlyList<bool> Cells => _cells;

    public GridData Surface =>
        BuildSurface(Palette.Single(Palette.ColorFor(1)), 1, 0);

    public static ShapeModel Empty(int width = 3, int height = 3) =>
        new(width, height, new bool[width * height]);

    public static ShapeModel SingleCell() => new(1, 1, new[] { true });

    /// <summary>Parses a <c>#</c>/<c>.</c> block exactly like the solver does.</summary>
    public static ShapeModel FromText(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && lines[0].Trim().Length == 0)
        {
            lines.RemoveAt(0);
        }

        if (lines.Count == 0)
        {
            throw new PuzzleFormatException("the shape is empty");
        }

        var indent = lines
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.TakeWhile(char.IsWhiteSpace).Count())
            .Min();

        var filled = new List<(int Row, int Col)>();
        for (var row = 0; row < lines.Count; row++)
        {
            var body = lines[row].Length > indent ? lines[row][indent..] : string.Empty;
            for (var col = 0; col < body.Length; col++)
            {
                switch (body[col])
                {
                    case '#':
                        filled.Add((row, col));
                        break;
                    case '.':
                    case ' ':
                        break;
                    default:
                        throw new PuzzleFormatException(
                            $"unexpected character '{body[col]}' at row {row + 1}, column {col + 1} " +
                            "(use '#' for filled cells and '.' for empty ones)");
                }
            }
        }

        if (filled.Count == 0)
        {
            throw new PuzzleFormatException("the shape has no '#' cells");
        }

        var minRow = filled.Min(cell => cell.Row);
        var maxRow = filled.Max(cell => cell.Row);
        var minCol = filled.Min(cell => cell.Col);
        var maxCol = filled.Max(cell => cell.Col);
        var width = maxCol - minCol + 1;
        var height = maxRow - minRow + 1;
        var cells = new bool[width * height];
        foreach (var (row, col) in filled)
        {
            cells[(row - minRow) * width + (col - minCol)] = true;
        }

        return new ShapeModel(width, height, cells);
    }

    public ShapeModel Clone() => new(_width, _height, (bool[])_cells.Clone());

    public void SetCell(int row, int col, bool filled)
    {
        if (row < 0 || row >= _height || col < 0 || col >= _width)
        {
            return;
        }

        var index = row * _width + col;
        if (_cells[index] == filled)
        {
            return;
        }

        _cells[index] = filled;
        RaiseChanged();
    }

    public void SetAll(bool filled)
    {
        Array.Fill(_cells, filled);
        RaiseChanged();
    }

    public void Resize(int width, int height)
    {
        width = Math.Clamp(width, 1, 64);
        height = Math.Clamp(height, 1, 64);
        if (width == _width && height == _height)
        {
            return;
        }

        var cells = new bool[width * height];
        var copyWidth = Math.Min(width, _width);
        var copyHeight = Math.Min(height, _height);
        for (var row = 0; row < copyHeight; row++)
        {
            for (var col = 0; col < copyWidth; col++)
            {
                cells[row * width + col] = _cells[row * _width + col];
            }
        }

        _width = width;
        _height = height;
        _cells = cells;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        RaiseChanged();
    }

    /// <summary>Rotates the shape 90 degrees clockwise.</summary>
    public void RotateRight()
    {
        var width = _height;
        var height = _width;
        var cells = new bool[width * height];
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
            {
                if (_cells[row * _width + col])
                {
                    var targetRow = col;
                    var targetCol = _height - 1 - row;
                    cells[targetRow * width + targetCol] = true;
                }
            }
        }

        _width = width;
        _height = height;
        _cells = cells;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        RaiseChanged();
    }

    /// <summary>Rotates the shape 90 degrees counter-clockwise.</summary>
    public void RotateLeft()
    {
        var width = _height;
        var height = _width;
        var cells = new bool[width * height];
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
            {
                if (_cells[row * _width + col])
                {
                    var targetRow = _width - 1 - col;
                    var targetCol = row;
                    cells[targetRow * width + targetCol] = true;
                }
            }
        }

        _width = width;
        _height = height;
        _cells = cells;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        RaiseChanged();
    }

    /// <summary>Mirrors the shape horizontally.</summary>
    public void Mirror()
    {
        var cells = new bool[_cells.Length];
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
            {
                if (_cells[row * _width + col])
                {
                    cells[row * _width + (_width - 1 - col)] = true;
                }
            }
        }

        _cells = cells;
        RaiseChanged();
    }

    /// <summary>Removes empty border rows and columns.</summary>
    public void Trim()
    {
        var minRow = int.MaxValue;
        var maxRow = -1;
        var minCol = int.MaxValue;
        var maxCol = -1;
        for (var row = 0; row < _height; row++)
        {
            for (var col = 0; col < _width; col++)
            {
                if (!_cells[row * _width + col])
                {
                    continue;
                }

                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
                minCol = Math.Min(minCol, col);
                maxCol = Math.Max(maxCol, col);
            }
        }

        if (maxRow < 0)
        {
            _width = 1;
            _height = 1;
            _cells = new bool[1];
            OnPropertyChanged(nameof(Width));
            OnPropertyChanged(nameof(Height));
            RaiseChanged();
            return;
        }

        var width = maxCol - minCol + 1;
        var height = maxRow - minRow + 1;
        var cells = new bool[width * height];
        for (var row = minRow; row <= maxRow; row++)
        {
            for (var col = minCol; col <= maxCol; col++)
            {
                cells[(row - minRow) * width + (col - minCol)] = _cells[row * _width + col];
            }
        }

        _width = width;
        _height = height;
        _cells = cells;
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        RaiseChanged();
    }

    public bool IsConnected()
    {
        var start = Array.IndexOf(_cells, true);
        if (start < 0)
        {
            return false;
        }

        var seen = new bool[_cells.Length];
        var stack = new Stack<int>();
        stack.Push(start);
        seen[start] = true;
        var visited = 1;
        while (stack.Count > 0)
        {
            var index = stack.Pop();
            var row = index / _width;
            var col = index % _width;
            foreach (var (nr, nc) in new[]
                     {
                         (row - 1, col), (row + 1, col), (row, col - 1), (row, col + 1),
                     })
            {
                if (nr < 0 || nr >= _height || nc < 0 || nc >= _width)
                {
                    continue;
                }

                var next = nr * _width + nc;
                if (_cells[next] && !seen[next])
                {
                    seen[next] = true;
                    visited++;
                    stack.Push(next);
                }
            }
        }

        return visited == Area;
    }

    public string[] ToRows()
    {
        var rows = new string[_height];
        for (var row = 0; row < _height; row++)
        {
            var chars = new char[_width];
            for (var col = 0; col < _width; col++)
            {
                chars[col] = _cells[row * _width + col] ? '#' : '.';
            }

            rows[row] = new string(chars);
        }

        return rows;
    }

    public string ToShapeText() => string.Join('\n', ToRows());

    public GridData BuildSurface(IReadOnlyList<Color> palette, int colorIndex, int labelIndex)
    {
        var cells = new int[_cells.Length];
        var labels = new string[_cells.Length];
        var label = labelIndex > 0 ? labelIndex.ToString() : string.Empty;
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i])
            {
                cells[i] = colorIndex;
                labels[i] = label;
            }
        }

        return new GridData(_width, _height, cells, labels, palette);
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(Surface));
        OnPropertyChanged(nameof(Area));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
