using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PuzzlerGui.Models;

public sealed partial class PuzzleDocument : ObservableObject
{
    [ObservableProperty]
    private string _name = "untitled";

    [ObservableProperty]
    private bool _allUse = true;

    [ObservableProperty]
    private bool _canRotate = true;

    [ObservableProperty]
    private bool _canFlip = false;

    public PuzzleDocument(string name, BoardModel board)
    {
        _name = name;
        Board = board;
        Pieces = new ObservableCollection<PieceModel>();
        Board.Changed += OnChildChanged;
        Pieces.CollectionChanged += OnPiecesChanged;
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(TotalPieceCells) or nameof(PieceTypes))
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public BoardModel Board { get; }

    public ObservableCollection<PieceModel> Pieces { get; }

    public int TotalPieceCells => Pieces.Sum(piece => piece.Area * piece.Count);

    public int PieceTypes => Pieces.Count;

    public static PuzzleDocument CreateDefault()
    {
        var document = new PuzzleDocument("untitled", BoardModel.Rectangle(4, 4))
        {
            AllUse = true,
            CanRotate = true,
            CanFlip = false,
        };
        document.Pieces.Add(new PieceModel("domino", ShapeModel.FromText("##"), 8));
        return document;
    }

    public void Validate(out IReadOnlyList<string> errors, out IReadOnlyList<string> warnings)
    {
        var errorList = new List<string>();
        var warningList = new List<string>();

        if (Board.PlayableCells == 0)
        {
            errorList.Add("board_shape: the board is empty");
        }

        if (Pieces.Count == 0)
        {
            errorList.Add("puzzle: no pieces were defined");
        }

        foreach (var piece in Pieces)
        {
            if (piece.Area == 0)
            {
                errorList.Add($"piece {piece.Id}: the shape is empty");
                continue;
            }

            if (!piece.Shape.IsConnected())
            {
                errorList.Add($"piece {piece.Id}: the shape is not 4-connected, so it cannot be a physical piece");
            }

            if (piece.Area > Board.PlayableCells)
            {
                errorList.Add($"piece {piece.Id}: covers {piece.Area} cells but the board only has {Board.PlayableCells}");
            }

            if (piece.Count < 1)
            {
                errorList.Add($"piece {piece.Id}: count must be at least 1");
            }
        }

        if (Board.PlayableCells > 0)
        {
            var total = TotalPieceCells;
            if (total < Board.PlayableCells)
            {
                errorList.Add(
                    $"the pieces cover {total} cells but the board needs {Board.PlayableCells}: " +
                    "no arrangement can fill it");
            }

            if (AllUse && total != Board.PlayableCells && total >= Board.PlayableCells)
            {
                errorList.Add(
                    $"all_use is true, so every piece must be placed, but the pieces cover {total} cells " +
                    $"while the board has {Board.PlayableCells} (turn off all_use to allow unused pieces)");
            }

            if (!Board.IsConnected())
            {
                warningList.Add(
                    "board_shape: the board is not 4-connected; it will only be solvable if pieces can span the gap");
            }
        }

        var duplicateIds = Pieces
            .GroupBy(piece => piece.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            warningList.Add($"duplicate piece id(s): {string.Join(", ", duplicateIds)}");
        }

        errors = errorList;
        warnings = warningList;
    }

    private void OnChildChanged(object? sender, EventArgs e) => RaiseChanged();

    private void OnPiecesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PieceModel piece in e.OldItems)
            {
                Detach(piece);
            }
        }

        if (e.NewItems is not null)
        {
            foreach (PieceModel piece in e.NewItems)
            {
                Attach(piece);
            }
        }

        OnPropertyChanged(nameof(TotalPieceCells));
        OnPropertyChanged(nameof(PieceTypes));
        RaiseChanged();
    }

    private void Attach(PieceModel piece) => piece.PropertyChanged += OnChildChanged;

    private void Detach(PieceModel piece) => piece.PropertyChanged -= OnChildChanged;

    private void RaiseChanged()
    {
        OnPropertyChanged(nameof(TotalPieceCells));
        OnPropertyChanged(nameof(PieceTypes));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
