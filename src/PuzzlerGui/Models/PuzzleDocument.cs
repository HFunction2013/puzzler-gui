using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PuzzlerGui.Services;

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

    public PuzzleDocument Clone()
    {
        var copy = new PuzzleDocument(Name, Board.Clone())
        {
            AllUse = AllUse,
            CanRotate = CanRotate,
            CanFlip = CanFlip,
        };

        foreach (var piece in Pieces)
        {
            copy.Pieces.Add(piece.Clone());
        }

        return copy;
    }

    public void Validate(out IReadOnlyList<string> errors, out IReadOnlyList<string> warnings)
    {
        var errorList = new List<string>();
        var warningList = new List<string>();

        if (Board.PlayableCells == 0)
        {
            errorList.Add(L("validation.boardEmpty"));
        }

        if (Pieces.Count == 0)
        {
            errorList.Add(L("validation.noPieces"));
        }

        foreach (var piece in Pieces)
        {
            if (piece.Area == 0)
            {
                errorList.Add(L("validation.pieceEmptyShape", piece.Id));
                continue;
            }

            if (!piece.Shape.IsConnected())
            {
                errorList.Add(L("validation.pieceDisconnected", piece.Id));
            }

            if (piece.Area > Board.PlayableCells)
            {
                errorList.Add(L("validation.pieceTooLarge", piece.Id, piece.Area, Board.PlayableCells));
            }

            if (piece.Count < 1)
            {
                errorList.Add(L("validation.pieceBadCount", piece.Id));
            }
        }

        if (Board.PlayableCells > 0)
        {
            var total = TotalPieceCells;
            if (total < Board.PlayableCells)
            {
                errorList.Add(L("validation.areaShort", total, Board.PlayableCells));
            }

            if (AllUse && total != Board.PlayableCells && total >= Board.PlayableCells)
            {
                errorList.Add(L("validation.allUseMismatch", total, Board.PlayableCells));
            }

            if (!Board.IsConnected())
            {
                warningList.Add(L("validation.boardDisconnected"));
            }
        }

        var duplicateIds = Pieces
            .GroupBy(piece => piece.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            warningList.Add(L("validation.duplicateIds", string.Join(", ", duplicateIds)));
        }

        errors = errorList;
        warnings = warningList;
    }

    private static string L(string key, params object?[] args) =>
        string.Format(LocalizationService.Instance[key], args);

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
