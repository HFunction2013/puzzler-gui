using CommunityToolkit.Mvvm.ComponentModel;
using PuzzlerGui.Services;

namespace PuzzlerGui.Models;

/// <summary>The playable region of the puzzle.</summary>
public sealed class BoardModel : ObservableObject
{
    private int _width;
    private int _height;
    private bool[] _cells;

    public BoardModel(int width, int height, bool[] cells)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The board needs at least one row and one column.");
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

    public int PlayableCells => _cells.Count(c => c);

    public bool AllPlayable => _cells.All(c => c);

    public GridData Surface => BuildSurface();

    public static BoardModel Rectangle(int width, int height) =>
        new(width, height, Enumerable.Repeat(true, width * height).ToArray());

    /// <summary>Parses an ASCII board, trimming the surrounding empty border.</summary>
    public static BoardModel FromAscii(IReadOnlyList<string> rows)
    {
        var minRow = int.MaxValue;
        var maxRow = -1;
        var minCol = int.MaxValue;
        var maxCol = -1;

        for (var row = 0; row < rows.Count; row++)
        {
            var line = rows[row];
            for (var col = 0; col < line.Length; col++)
            {
                switch (line[col])
                {
                    case '#':
                        minRow = Math.Min(minRow, row);
                        maxRow = Math.Max(maxRow, row);
                        minCol = Math.Min(minCol, col);
                        maxCol = Math.Max(maxCol, col);
                        break;
                    case '.':
                    case ' ':
                        break;
                    default:
                        throw new PuzzleFormatException(
                            $"unexpected character '{line[col]}' at row {row + 1}, column {col + 1} " +
                            "(use '#' for playable cells and '.' for blocked ones)");
                }
            }
        }

        if (maxRow < 0)
        {
            throw new PuzzleFormatException("the board contains no '#' cells");
        }

        var width = maxCol - minCol + 1;
        var height = maxRow - minRow + 1;
        var cells = new bool[width * height];
        for (var row = minRow; row <= maxRow; row++)
        {
            var line = rows[row];
            for (var col = minCol; col <= maxCol; col++)
            {
                cells[(row - minRow) * width + (col - minCol)] =
                    col < line.Length && line[col] == '#';
            }
        }

        return new BoardModel(width, height, cells);
    }

    public bool IsPlayable(int row, int col) =>
        row >= 0 && row < _height && col >= 0 && col < _width && _cells[row * _width + col];

    public IReadOnlyList<bool> Cells => _cells;

    public BoardModel Clone() => new(_width, _height, (bool[])_cells.Clone());

    public void SetCell(int row, int col, bool playable)
    {
        if (row < 0 || row >= _height || col < 0 || col >= _width)
        {
            return;
        }

        var index = row * _width + col;
        if (_cells[index] == playable)
        {
            return;
        }

        _cells[index] = playable;
        RaiseChanged();
    }

    public void SetAll(bool playable)
    {
        Array.Fill(_cells, playable);
        RaiseChanged();
    }

    public void Resize(int width, int height)
    {
        width = Math.Clamp(width, 1, 128);
        height = Math.Clamp(height, 1, 128);
        if (width == _width && height == _height)
        {
            return;
        }

        // Growing a rectangular board keeps the new cells playable; growing a
        // custom shape leaves them blocked so the user can paint them in.
        var wasRectangular = AllPlayable;
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

        if (wasRectangular)
        {
            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < width; col++)
                {
                    cells[row * width + col] = true;
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

    public string[] ToAsciiRows()
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
        var total = PlayableCells;

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

        return visited == total;
    }

    private GridData BuildSurface()
    {
        var cells = new int[_cells.Length];
        var labels = new string[_cells.Length];
        for (var i = 0; i < _cells.Length; i++)
        {
            cells[i] = _cells[i] ? 1 : 0;
        }

        return new GridData(_width, _height, cells, labels, Palette.Single(Palette.BoardCell));
    }

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(Surface));
        OnPropertyChanged(nameof(PlayableCells));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
