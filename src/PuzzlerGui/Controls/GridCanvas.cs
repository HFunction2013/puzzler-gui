using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using PuzzlerGui.Models;
using PuzzlerGui.Services;

namespace PuzzlerGui.Controls;

public sealed class CellPaintEventArgs : EventArgs
{
    public CellPaintEventArgs(int row, int column, bool filled)
    {
        Row = row;
        Column = column;
        Filled = filled;
    }

    public int Row { get; }

    public int Column { get; }

    public bool Filled { get; }
}

/// <summary>
/// Draws a cell grid and, when editable, lets the pointer paint cells. It is
/// used for the board, the piece shapes and the solution preview.
/// </summary>
public sealed class GridCanvas : Control
{
    public static readonly StyledProperty<GridData?> DataProperty =
        AvaloniaProperty.Register<GridCanvas, GridData?>(nameof(Data));

    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<GridCanvas, bool>(nameof(IsEditable));

    public static readonly StyledProperty<bool> ShowLabelsProperty =
        AvaloniaProperty.Register<GridCanvas, bool>(nameof(ShowLabels), true);

    public static readonly StyledProperty<double> MaxCellSizeProperty =
        AvaloniaProperty.Register<GridCanvas, double>(nameof(MaxCellSize), 48.0);

    private const double Padding = 12.0;
    private const double MinCellSize = 2.0;

    private double _cellSize;
    private Point _origin;
    private bool _painting;
    private bool _paintValue;
    private (int Row, int Column)? _hover;
    private (int Row, int Column)? _lastPainted;

    static GridCanvas()
    {
        AffectsRender<GridCanvas>(DataProperty, IsEditableProperty, ShowLabelsProperty, MaxCellSizeProperty);
        AffectsMeasure<GridCanvas>(DataProperty, MaxCellSizeProperty);
    }

    public GridCanvas()
    {
        Focusable = true;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public event EventHandler<CellPaintEventArgs>? CellPaintRequested;

    public GridData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public bool IsEditable
    {
        get => GetValue(IsEditableProperty);
        set => SetValue(IsEditableProperty, value);
    }

    public bool ShowLabels
    {
        get => GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double MaxCellSize
    {
        get => GetValue(MaxCellSizeProperty);
        set => SetValue(MaxCellSizeProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 320 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 240 : availableSize.Height;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var data = Data;
        var bounds = new Rect(Bounds.Size);
        if (data is null || data.Cols <= 0 || data.Rows <= 0)
        {
            _cellSize = 0;
            return;
        }

        var padding = Math.Clamp(Math.Min(bounds.Width, bounds.Height) * 0.06, 2.0, Padding);
        var availableWidth = Math.Max(bounds.Width - padding * 2, 1);
        var availableHeight = Math.Max(bounds.Height - padding * 2, 1);
        var cell = Math.Min(availableWidth / data.Cols, availableHeight / data.Rows);
        cell = Math.Min(cell, MaxCellSize);
        cell = Math.Max(cell, MinCellSize);
        _cellSize = cell;

        var gridWidth = cell * data.Cols;
        var gridHeight = cell * data.Rows;
        _origin = new Point(
            (bounds.Width - gridWidth) / 2,
            (bounds.Height - gridHeight) / 2);

        var dark = ActualThemeVariant == ThemeVariant.Dark;
        var blockedBrush = new SolidColorBrush(dark ? Color.Parse("#1B2130") : Color.Parse("#EDF1F6"));
        var gridPen = new Pen(new SolidColorBrush(dark ? Color.Parse("#334156") : Color.Parse("#CBD5E1")), 1);
        var blockedPen = new Pen(new SolidColorBrush(dark ? Color.Parse("#2B3446") : Color.Parse("#DCE4EE")), 1);

        for (var row = 0; row < data.Rows; row++)
        {
            for (var col = 0; col < data.Cols; col++)
            {
                var index = row * data.Cols + col;
                var value = data.Cells[index];
                var rect = new Rect(
                    _origin.X + col * cell,
                    _origin.Y + row * cell,
                    cell,
                    cell);

                if (value <= 0)
                {
                    context.DrawRectangle(blockedBrush, blockedPen, rect);
                    if (IsEditable && cell >= 10)
                    {
                        var radius = Math.Min(1.5, cell * 0.06);
                        var center = rect.Center;
                        context.DrawEllipse(
                            new SolidColorBrush(dark ? Color.Parse("#4B5B70") : Color.Parse("#B9C6D6")),
                            null,
                            center,
                            radius,
                            radius);
                    }

                    continue;
                }

                var color = data.ColorAt(value);
                var brush = new SolidColorBrush(color);
                context.DrawRectangle(brush, gridPen, rect);

                if (ShowLabels && cell >= 14)
                {
                    var label = index < data.Labels.Length ? data.Labels[index] : string.Empty;
                    if (label.Length > 0)
                    {
                        var textBrush = new SolidColorBrush(Palette.TextColorFor(color));
                        var fontSize = Math.Clamp(cell * 0.42, 8.0, 22.0);
                        var formatted = new FormattedText(
                            label,
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(FontFamily.Default),
                            fontSize,
                            textBrush);
                        context.DrawText(
                            formatted,
                            new Point(
                                rect.Center.X - formatted.Width / 2,
                                rect.Center.Y - formatted.Height / 2));
                    }
                }
            }
        }

        if (_hover is { } hover && IsEditable)
        {
            var hoverRect = new Rect(
                _origin.X + hover.Column * cell,
                _origin.Y + hover.Row * cell,
                cell,
                cell);
            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Color.Parse("#FFB04A")), Math.Max(2.0, cell * 0.06)),
                hoverRect);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEditable || Data is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (HitTest(e.GetPosition(this)) is not { } cell)
            {
                return;
            }

            var index = cell.Row * Data.Cols + cell.Column;
            _paintValue = Data.Cells[index] <= 0;
            _painting = true;
            _lastPainted = cell;
            RaisePaint(cell, _paintValue);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            if (HitTest(e.GetPosition(this)) is not { } cell)
            {
                return;
            }

            _paintValue = false;
            _painting = true;
            _lastPainted = cell;
            RaisePaint(cell, false);
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Data is null || _cellSize <= 0)
        {
            return;
        }

        var hover = HitTest(e.GetPosition(this));
        if (hover != _hover)
        {
            _hover = hover;
            InvalidateVisual();
        }

        if (!_painting || !IsEditable)
        {
            return;
        }

        if (hover is { } cell && cell != _lastPainted)
        {
            _lastPainted = cell;
            RaisePaint(cell, _paintValue);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _painting = false;
        _lastPainted = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hover is not null)
        {
            _hover = null;
            InvalidateVisual();
        }
    }

    private void RaisePaint((int Row, int Column) cell, bool filled)
    {
        CellPaintRequested?.Invoke(this, new CellPaintEventArgs(cell.Row, cell.Column, filled));
    }

    private (int Row, int Column)? HitTest(Point point)
    {
        var data = Data;
        if (data is null || _cellSize <= 0)
        {
            return null;
        }

        var col = (int)Math.Floor((point.X - _origin.X) / _cellSize);
        var row = (int)Math.Floor((point.Y - _origin.Y) / _cellSize);
        if (row < 0 || row >= data.Rows || col < 0 || col >= data.Cols)
        {
            return null;
        }

        return (row, col);
    }
}
