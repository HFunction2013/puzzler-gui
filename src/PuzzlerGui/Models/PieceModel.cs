using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PuzzlerGui.Services;

namespace PuzzlerGui.Models;

/// <summary>One piece type, including the per-piece option overrides.</summary>
public sealed partial class PieceModel : ObservableObject
{
    [ObservableProperty]
    private string _id = "piece";

    [ObservableProperty]
    private int _count = 1;

    [ObservableProperty]
    private TriState _canRotate = TriState.Inherit;

    [ObservableProperty]
    private TriState _canFlip = TriState.Inherit;

    [ObservableProperty]
    private int _colorIndex = 1;

    private ShapeModel _shape;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public PieceModel(string id, ShapeModel shape, int count = 1)
    {
        _id = id;
        _shape = shape;
        _count = count;
        _shape.Changed += OnShapeChanged;
        _loc.PropertyChanged += OnLocalizationChanged;
    }

    public ShapeModel Shape
    {
        get => _shape;
        set
        {
            if (ReferenceEquals(_shape, value))
            {
                return;
            }

            _shape.Changed -= OnShapeChanged;
            _shape = value;
            _shape.Changed += OnShapeChanged;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Surface));
            OnPropertyChanged(nameof(Area));
        }
    }

    public int Area => _shape.Area;

    public GridData Surface => _shape.BuildSurface(Palette.All, ColorIndex, 0);

    public Avalonia.Media.IBrush Brush => new Avalonia.Media.SolidColorBrush(Palette.ColorFor(ColorIndex));

    public string CellSummary => string.Format(_loc["piece.summary"], Area, Count);

    public PieceModel Clone() =>
        new(Id, Shape.Clone(), Count) { CanRotate = CanRotate, CanFlip = CanFlip };

    partial void OnColorIndexChanged(int value)
    {
        OnPropertyChanged(nameof(Surface));
        OnPropertyChanged(nameof(Brush));
    }

    partial void OnCountChanged(int value) => OnPropertyChanged(nameof(CellSummary));

    private void OnShapeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Surface));
        OnPropertyChanged(nameof(Area));
        OnPropertyChanged(nameof(CellSummary));
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(nameof(CellSummary));
}
