using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PuzzlerGui.Models;
using PuzzlerGui.Services;

namespace PuzzlerGui.ViewModels;

public enum BannerKind
{
    None,
    Info,
    Success,
    Warning,
    Error,
}


public sealed record PlacementRow(string Text, IBrush Brush);

internal sealed record DocumentSnapshot(PuzzleDocument Document, string? SelectedPieceId);

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly PuzzlerRunner _runner;
    private readonly DispatcherTimer _timer;
    private readonly Stack<DocumentSnapshot> _undoStack = new();
    private readonly Stack<DocumentSnapshot> _redoStack = new();
    private DocumentSnapshot? _baseline;
    private PuzzleDocument _document;
    private PieceModel? _selectedPiece;
    private GridData? _boardSurface;
    private GridData? _pieceSurface;
    private GridData? _solutionSurface;
    private CancellationTokenSource? _solveCancellation;
    private SolveReport? _lastReport;
    private string _lastJson = string.Empty;
    private DateTimeOffset _solveStarted;
    private string? _currentFilePath;
    private bool _isDirty;
    private bool _isSolving;
    private bool _suppressDocumentEvents;
    private int _revision;
    private int _solveRevision;
    private int _solutionCount;
    private int _selectedSolutionIndex;
    private string _statusText = "Ready";
    private string _bannerText = string.Empty;
    private BannerKind _bannerKind = BannerKind.None;
    private string _validationText = "Puzzle looks valid.";
    private bool _validationValid = true;
    private string _statLine = string.Empty;
    private string _multiplicityText = string.Empty;
    private bool _solutionsStale;
    private int _selectedTabIndex;
    private string _elapsedText = string.Empty;
    private decimal? _limitSolutions;
    private decimal? _nodeLimit;
    private decimal? _timeLimit;
    private decimal? _seedValue;
    private bool _dedupe = true;
    private bool _compressJson = true;

    public MainWindowViewModel(AppSettings settings)
    {
        Settings = settings;
        Loc.Apply(settings.Language);
        _runner = new PuzzlerRunner(() => Settings.SolverPath);
        _document = PuzzleDocument.CreateDefault();
        _document.Changed += OnDocumentChanged;
        Placements = new ObservableCollection<PlacementRow>();
        RecentFiles = new ObservableCollection<string>(settings.RecentFiles);
        TriStateOptions = new[] { TriState.Inherit, TriState.Allow, TriState.Forbid };

        SelectedPiece = _document.Pieces.FirstOrDefault();
        RefreshDocumentState(markDirty: false);
        _baseline = CaptureSnapshot();
        UpdateSolverStatus();
        StatusText = Loc["status.ready"];
        ValidationText = Loc["solver.validationFallback"];
        Loc.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(LocalizationService.Strings) or "Item[]")
            {
                OnPropertyChanged(nameof(BoardSummary));
                OnPropertyChanged(nameof(EmptyHintText));
                OnPropertyChanged(nameof(SolutionsTabHeader));
                OnPropertyChanged(nameof(SolverStatusText));
            }
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTimeOffset.Now - _solveStarted;
            ElapsedText = $"{elapsed.TotalSeconds:0.0} s";
        };
    }

    public AppSettings Settings { get; }

    public LocalizationService Loc => LocalizationService.Instance;

    public IReadOnlyList<TriState> TriStateOptions { get; }

    public ObservableCollection<PlacementRow> Placements { get; }

    public ObservableCollection<string> RecentFiles { get; }

    public PuzzleDocument Document => _document;

    public PieceModel? SelectedPiece
    {
        get => _selectedPiece;
        set
        {
            if (!SetProperty(ref _selectedPiece, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelectedPiece));
            OnPropertyChanged(nameof(SelectedPieceId));
            OnPropertyChanged(nameof(PieceCountValue));
            OnPropertyChanged(nameof(ShapeWidthValue));
            OnPropertyChanged(nameof(ShapeHeightValue));
            RefreshPieceSurface();
            UpdateCommandStates();
        }
    }

    public bool HasSelectedPiece => _selectedPiece is not null;

    public string SelectedPieceId
    {
        get => _selectedPiece?.Id ?? string.Empty;
        set
        {
            if (_selectedPiece is null || _selectedPiece.Id == value)
            {
                return;
            }

            _selectedPiece.Id = value;
            OnPropertyChanged();
        }
    }

    public decimal? PieceCountValue
    {
        get => _selectedPiece?.Count;
        set
        {
            if (_selectedPiece is null || value is null)
            {
                return;
            }

            var count = (int)Math.Clamp(Math.Round(value.Value), 1m, 65535m);
            if (_selectedPiece.Count == count)
            {
                return;
            }

            _selectedPiece.Count = count;
            OnPropertyChanged();
        }
    }

    public decimal? BoardWidthValue
    {
        get => _document.Board.Width;
        set
        {
            if (value is null)
            {
                return;
            }

            var width = (int)Math.Clamp(Math.Round(value.Value), 1m, 128m);
            if (width == _document.Board.Width)
            {
                return;
            }

            _document.Board.Resize(width, _document.Board.Height);
            OnPropertyChanged();
        }
    }

    public decimal? BoardHeightValue
    {
        get => _document.Board.Height;
        set
        {
            if (value is null)
            {
                return;
            }

            var height = (int)Math.Clamp(Math.Round(value.Value), 1m, 128m);
            if (height == _document.Board.Height)
            {
                return;
            }

            _document.Board.Resize(_document.Board.Width, height);
            OnPropertyChanged();
        }
    }

    public decimal? ShapeWidthValue
    {
        get => _selectedPiece?.Shape.Width;
        set
        {
            if (_selectedPiece is null || value is null)
            {
                return;
            }

            var width = (int)Math.Clamp(Math.Round(value.Value), 1m, 64m);
            if (width == _selectedPiece.Shape.Width)
            {
                return;
            }

            _selectedPiece.Shape.Resize(width, _selectedPiece.Shape.Height);
            OnPropertyChanged();
        }
    }

    public decimal? ShapeHeightValue
    {
        get => _selectedPiece?.Shape.Height;
        set
        {
            if (_selectedPiece is null || value is null)
            {
                return;
            }

            var height = (int)Math.Clamp(Math.Round(value.Value), 1m, 64m);
            if (height == _selectedPiece.Shape.Height)
            {
                return;
            }

            _selectedPiece.Shape.Resize(_selectedPiece.Shape.Width, height);
            OnPropertyChanged();
        }
    }

    public GridData? BoardSurface
    {
        get => _boardSurface;
        private set => SetProperty(ref _boardSurface, value);
    }

    public GridData? PieceSurface
    {
        get => _pieceSurface;
        private set => SetProperty(ref _pieceSurface, value);
    }

    public GridData? SolutionSurface
    {
        get => _solutionSurface;
        private set => SetProperty(ref _solutionSurface, value);
    }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set
        {
            if (SetProperty(ref _currentFilePath, value))
            {
                OnPropertyChanged(nameof(CurrentFileName));
                UpdateTitle();
            }
        }
    }

    public string CurrentFileName =>
        _currentFilePath is null ? "untitled" : Path.GetFileName(_currentFilePath);

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                UpdateTitle();
            }
        }
    }

    public string WindowTitle => $"puzzler-gui — {CurrentFileName}{(_isDirty ? " *" : string.Empty)}";

    public bool CanUndo => _undoStack.Count > 0 && !_isSolving;

    public bool CanRedo => _redoStack.Count > 0 && !_isSolving;

    public bool IsSolving
    {
        get => _isSolving;
        private set
        {
            if (!SetProperty(ref _isSolving, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNotSolving));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            UpdateCommandStates();
        }
    }

    public bool IsNotSolving => !_isSolving;

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string BannerText
    {
        get => _bannerText;
        private set
        {
            if (SetProperty(ref _bannerText, value))
            {
                OnPropertyChanged(nameof(HasBanner));
            }
        }
    }

    public BannerKind BannerKind
    {
        get => _bannerKind;
        private set
        {
            if (SetProperty(ref _bannerKind, value))
            {
                OnPropertyChanged(nameof(BannerBrush));
                OnPropertyChanged(nameof(BannerForeground));
            }
        }
    }

    public bool HasBanner => _bannerText.Length > 0;

    public IBrush BannerBrush => _bannerKind switch
    {
        BannerKind.Success => new SolidColorBrush(Color.Parse("#D9F2E3")),
        BannerKind.Warning => new SolidColorBrush(Color.Parse("#FFF1CC")),
        BannerKind.Error => new SolidColorBrush(Color.Parse("#FBDADA")),
        BannerKind.Info => new SolidColorBrush(Color.Parse("#DDE8FB")),
        _ => Brushes.Transparent,
    };

    public IBrush BannerForeground => _bannerKind switch
    {
        BannerKind.Success => new SolidColorBrush(Color.Parse("#155B32")),
        BannerKind.Warning => new SolidColorBrush(Color.Parse("#6B4A00")),
        BannerKind.Error => new SolidColorBrush(Color.Parse("#7A1B1B")),
        BannerKind.Info => new SolidColorBrush(Color.Parse("#173B6B")),
        _ => Brushes.Black,
    };

    public string ValidationText
    {
        get => _validationText;
        private set => SetProperty(ref _validationText, value);
    }

    public string BoardSummary =>
        $"{_document.Board.Width} × {_document.Board.Height} {Loc["board.summaryBoard"]}  ·  " +
        $"{_document.Board.PlayableCells} {Loc["board.playableCells"]}  ·  " +
        $"{_document.TotalPieceCells} cells across {_document.PieceTypes} {Loc["board.pieceTypes"]}";

    public bool ValidationValid
    {
        get => _validationValid;
        private set
        {
            if (SetProperty(ref _validationValid, value))
            {
                OnPropertyChanged(nameof(ValidationBrush));
            }
        }
    }

    public IBrush ValidationBrush => _validationValid
        ? new SolidColorBrush(Color.Parse("#2E7D4F"))
        : new SolidColorBrush(Color.Parse("#B02A2A"));

    public string StatLine
    {
        get => _statLine;
        private set => SetProperty(ref _statLine, value);
    }

    public string MultiplicityText
    {
        get => _multiplicityText;
        private set => SetProperty(ref _multiplicityText, value);
    }

    public bool SolutionsStale
    {
        get => _solutionsStale;
        private set => SetProperty(ref _solutionsStale, value);
    }

    public int SolutionCount
    {
        get => _solutionCount;
        private set
        {
            if (!SetProperty(ref _solutionCount, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSolutions));
            OnPropertyChanged(nameof(EmptyHintVisible));
            OnPropertyChanged(nameof(EmptyHintText));
            OnPropertyChanged(nameof(SolutionsTabHeader));
            OnPropertyChanged(nameof(SolutionIndexLabel));
            UpdateCommandStates();
        }
    }

    public bool HasSolutions => _solutionCount > 0;

    public bool EmptyHintVisible => !HasSolutions;

    public string EmptyHintText => _lastReport is null
        ? Loc["solution.emptyNoReport"]
        : Loc["solution.emptyNoSolution"];

    public string SolutionsTabHeader =>
        _solutionCount > 0
            ? string.Format(Loc["solution.tabCount"], _solutionCount)
            : Loc["tab.solutions"];

    public int SelectedSolutionIndex
    {
        get => _selectedSolutionIndex;
        private set
        {
            var clamped = _solutionCount == 0 ? 0 : Math.Clamp(value, 1, _solutionCount);
            if (!SetProperty(ref _selectedSolutionIndex, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(SolutionIndexLabel));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            UpdateSolutionView();
            UpdateCommandStates();
        }
    }

    public string SolutionIndexLabel =>
        _solutionCount == 0 ? "0 / 0" : $"{_selectedSolutionIndex} / {_solutionCount}";

    public bool CanGoPrevious => _selectedSolutionIndex > 1;

    public bool CanGoNext => _selectedSolutionIndex < _solutionCount;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public decimal? LimitSolutions
    {
        get => _limitSolutions;
        set => SetProperty(ref _limitSolutions, value);
    }

    public decimal? NodeLimit
    {
        get => _nodeLimit;
        set => SetProperty(ref _nodeLimit, value);
    }

    public decimal? TimeLimit
    {
        get => _timeLimit;
        set => SetProperty(ref _timeLimit, value);
    }

    public decimal? SeedValue
    {
        get => _seedValue;
        set => SetProperty(ref _seedValue, value);
    }

    public bool Dedupe
    {
        get => _dedupe;
        set => SetProperty(ref _dedupe, value);
    }

    public bool CompressJson
    {
        get => _compressJson;
        set => SetProperty(ref _compressJson, value);
    }

    public string SolverStatusText
    {
        get
        {
            var solver = _runner.FindSolver();
            return solver is null ? Loc["solver.notFound"] : Loc["solver.ready"];
        }
    }

    public string SolverPathText =>
        _runner.FindSolver() ?? "No puzzler executable was found next to the app or on PATH.";

    public bool SolverFound => _runner.FindSolver() is not null;

    public bool HasReport => _lastReport is not null;

    public string SamplesDirectory => Path.Combine(AppContext.BaseDirectory, "samples");

    public static string RecoveryFile => Path.Combine(SettingsStore.ConfigDirectory, "recovery.pzle");

    public static bool RecoveryFileExists => File.Exists(RecoveryFile);

    // ---------------------------------------------------------------- document

    public void NewDocument()
    {
        SetDocument(PuzzleDocument.CreateDefault(), null);
        ClearRecovery();
        StatusText = Loc["status.newPuzzle"];
        ClearBanner();
    }

    public bool LoadFile(string path, out string? error)
    {
        error = null;
        try
        {
            var text = File.ReadAllText(path);
            var document = PuzzleYaml.Load(text);
            SetDocument(document, Path.GetFullPath(path));
            ClearRecovery();
            StatusText = string.Format(Loc["status.opened"], Path.GetFileName(path));
            ClearBanner();
            AddRecent(path);
            return true;
        }
        catch (PuzzleFormatException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool SaveTo(string path, out string? error)
    {
        error = null;
        try
        {
            File.WriteAllText(path, PuzzleYaml.Save(_document));
            CurrentFilePath = Path.GetFullPath(path);
            IsDirty = false;
            ClearRecovery();
            StatusText = string.Format(Loc["status.saved"], Path.GetFileName(path));
            AddRecent(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public void SetSolverPath(string? path)
    {
        Settings.SolverPath = string.IsNullOrWhiteSpace(path) ? null : path;
        SettingsStore.Save(Settings);
        OnPropertyChanged(nameof(SolverStatusText));
        OnPropertyChanged(nameof(SolverFound));
        UpdateCommandStates();
    }

    public void AddRecent(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentFiles.FirstOrDefault(item =>
            string.Equals(Path.GetFullPath(item), fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentFiles.Remove(existing);
        }

        RecentFiles.Insert(0, fullPath);
        while (RecentFiles.Count > 8)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }

        Settings.RecentFiles = RecentFiles.ToList();
        SettingsStore.Save(Settings);
    }

    public static string? ReadRecoveryText() =>
        File.Exists(RecoveryFile) ? File.ReadAllText(RecoveryFile) : null;

    public bool TryLoadRecoveryText(string text)
    {
        try
        {
            SetDocument(PuzzleYaml.Load(text), null);
            ClearRecovery();
            StatusText = Loc["status.restored"];
            ClearBanner();
            return true;
        }
        catch (PuzzleFormatException)
        {
            ClearRecovery();
            return false;
        }
    }

    public void DiscardRecovery() => ClearRecovery();

    public void PrepareForCleanExit() => ClearRecovery();

    public void PaintBoardCell(int row, int column, bool filled) =>
        _document.Board.SetCell(row, column, filled);

    public void PaintPieceCell(int row, int column, bool filled) =>
        _selectedPiece?.Shape.SetCell(row, column, filled);

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        var current = CaptureSnapshot();
        var previous = _undoStack.Pop();
        _redoStack.Push(current);
        ApplySnapshot(previous);
        StatusText = Loc["status.undo"];
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        var current = CaptureSnapshot();
        var next = _redoStack.Pop();
        _undoStack.Push(current);
        ApplySnapshot(next);
        StatusText = Loc["status.redo"];
    }

    [RelayCommand]
    private void FillBoard() => _document.Board.SetAll(true);

    [RelayCommand]
    private void ClearBoard() => _document.Board.SetAll(false);

    [RelayCommand]
    private void AddPiece()
    {
        var index = 1;
        while (_document.Pieces.Any(piece => piece.Id == $"piece{index}"))
        {
            index++;
        }

        var piece = new PieceModel($"piece{index}", ShapeModel.FromText("##"));
        _document.Pieces.Add(piece);
        SelectedPiece = piece;
        StatusText = Loc["status.pieceAdded"];
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void RemovePiece()
    {
        if (_selectedPiece is null)
        {
            return;
        }

        var index = _document.Pieces.IndexOf(_selectedPiece);
        _document.Pieces.Remove(_selectedPiece);
        SelectedPiece = _document.Pieces.Count == 0
            ? null
            : _document.Pieces[Math.Clamp(index, 0, _document.Pieces.Count - 1)];
        StatusText = Loc["status.pieceRemoved"];
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void DuplicatePiece()
    {
        if (_selectedPiece is null)
        {
            return;
        }

        var copy = _selectedPiece.Clone();
        _document.Pieces.Insert(_document.Pieces.IndexOf(_selectedPiece) + 1, copy);
        SelectedPiece = copy;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void RotatePieceLeft() => _selectedPiece?.Shape.RotateLeft();

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void RotatePieceRight() => _selectedPiece?.Shape.RotateRight();

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void MirrorPiece() => _selectedPiece?.Shape.Mirror();

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void TrimPiece() => _selectedPiece?.Shape.Trim();

    [RelayCommand(CanExecute = nameof(HasSelectedPiece))]
    private void ClearPiece() => _selectedPiece?.Shape.SetAll(false);

    // ---------------------------------------------------------------- solving

    [RelayCommand(CanExecute = nameof(CanSolve))]
    private async Task SolveAsync()
    {
        ClearBanner();
        _document.Validate(out var errors, out var warnings);
        if (errors.Count > 0)
        {
            SetBanner(BannerKind.Error, string.Format(Loc["banner.fixErrors"], errors[0]));
            return;
        }

        if (!SolverFound)
        {
            SetBanner(BannerKind.Error, Loc["banner.solverMissing"]);
            return;
        }

        var parameters = BuildSolveParameters();
        _solveRevision = _revision;
        _solveCancellation = new CancellationTokenSource();
        _solveStarted = DateTimeOffset.Now;
        ElapsedText = "0.0 s";
        IsSolving = true;
        StatusText = Loc["status.solving"];
        _timer.Start();

        SolveOutcome outcome;
        try
        {
            outcome = await _runner.SolveAsync(_document, parameters, _solveCancellation.Token);
        }
        finally
        {
            _timer.Stop();
            IsSolving = false;
            _solveCancellation?.Dispose();
            _solveCancellation = null;
        }

        switch (outcome.Status)
        {
            case SolveStatus.Success:
                ApplyReport(outcome);
                var count = outcome.Report?.Solutions.Count ?? 0;
                var countText = count == 1
                    ? Loc["status.solutionsCountOne"]
                    : string.Format(Loc["status.solutionsCount"], count);
                StatusText = $"{countText} — {string.Format(Loc["stat.solvedIn"], outcome.Duration.TotalSeconds)}";
                if (outcome.Report?.Stats.Truncated == true)
                {
                    SetBanner(BannerKind.Warning, Loc["banner.truncated"]);
                }
                else
                {
                    SetBanner(BannerKind.Success,
                        count == 1
                            ? Loc["banner.solvedOne"]
                            : string.Format(Loc["banner.solved"], count));
                }

                SelectedTabIndex = 1;
                break;

            case SolveStatus.NoSolution:
                ApplyReport(outcome);
                StatusText = Loc["status.noSolution"];
                SetBanner(BannerKind.Warning, outcome.Message);
                SelectedTabIndex = 1;
                break;

            case SolveStatus.Cancelled:
                StatusText = Loc["status.cancelled"];
                SetBanner(BannerKind.Info, Loc["banner.searchCancelled"]);
                break;

            case SolveStatus.SolverMissing:
                SetBanner(BannerKind.Error, outcome.Message);
                break;

            case SolveStatus.InvalidPuzzle:
            case SolveStatus.Failed:
            default:
                StatusText = Loc["status.solveFailed"];
                SetBanner(BannerKind.Error, outcome.Message);
                break;
        }
    }

    private bool CanSolve() => !_isSolving;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelSolve()
    {
        _solveCancellation?.Cancel();
        StatusText = Loc["status.cancelling"];
    }

    private bool CanCancel() => _isSolving;

    [RelayCommand(CanExecute = nameof(IsNotSolving))]
    private async Task ValidateWithSolverAsync()
    {
        ClearBanner();
        var outcome = await _runner.ValidateAsync(_document, CancellationToken.None);
        SetBanner(
            outcome.IsValid ? BannerKind.Success : BannerKind.Error,
            outcome.IsValid ? Loc["banner.validationAccepted"] : outcome.Output);
        StatusText = outcome.IsValid ? Loc["status.validationPassed"] : Loc["status.validationFailed"];
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void PreviousSolution() => SelectedSolutionIndex--;

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextSolution() => SelectedSolutionIndex++;

    [RelayCommand(CanExecute = nameof(HasSolutions))]
    private void FirstSolution() => SelectedSolutionIndex = 1;

    [RelayCommand(CanExecute = nameof(HasSolutions))]
    private void LastSolution() => SelectedSolutionIndex = _solutionCount;

    public void JumpToSolution(int index) => SelectedSolutionIndex = index;

    public bool SaveReportJson(string path, out string? error)
    {
        error = null;
        if (_lastReport is null)
        {
            error = Loc["export.noReport"];
            return false;
        }

        try
        {
            File.WriteAllText(path, RenderReportJson(_lastReport, _lastJson, _compressJson));

            StatusText = string.Format(Loc["status.exported"], Path.GetFileName(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static string RenderReportJson(SolveReport? report, string rawJson, bool compressJson)
    {
        if (!string.IsNullOrWhiteSpace(rawJson))
        {
            return rawJson;
        }

        if (report is null)
        {
            return string.Empty;
        }

        var options = new JsonSerializerOptions { WriteIndented = !compressJson };
        return JsonSerializer.Serialize(report, options) + Environment.NewLine;
    }

    public void ClearBanner()
    {
        BannerText = string.Empty;
        BannerKind = BannerKind.None;
    }

    public void SetBanner(BannerKind kind, string text)
    {
        BannerKind = kind;
        BannerText = text;
    }

    // ------------------------------------------------------------- internals

    private SolveParameters BuildSolveParameters()
    {
        return new SolveParameters
        {
            MaxSolutions = _limitSolutions is > 0 ? (int)_limitSolutions.Value : null,
            NodeLimit = _nodeLimit is > 0 ? (long)_nodeLimit.Value : null,
            TimeLimitSeconds = _timeLimit is > 0 ? (double)_timeLimit.Value : null,
            Dedupe = _dedupe,
            CompressJson = _compressJson,
            Seed = _seedValue is >= 0 ? (ulong)_seedValue.Value : null,
        };
    }

    private void SetDocument(PuzzleDocument document, string? path)
    {
        _document.Changed -= OnDocumentChanged;
        _document = document;
        _document.Changed += OnDocumentChanged;
        OnPropertyChanged(nameof(Document));
        CurrentFilePath = path;
        IsDirty = false;
        SelectedPiece = document.Pieces.FirstOrDefault();
        RefreshDocumentState(markDirty: false);
        ClearReport();
        OnPropertyChanged(nameof(BoardWidthValue));
        OnPropertyChanged(nameof(BoardHeightValue));
        _undoStack.Clear();
        _redoStack.Clear();
        _baseline = CaptureSnapshot();
        UpdateCommandStates();
    }

    private void OnDocumentChanged(object? sender, EventArgs e)
    {
        if (_suppressDocumentEvents)
        {
            return;
        }

        _revision++;
        if (!SameAsBaseline())
        {
            _undoStack.Push(_baseline ?? CaptureSnapshot());
            _redoStack.Clear();
            _baseline = CaptureSnapshot();
        }

        RefreshDocumentState(markDirty: true);
        PersistRecovery();
        if (_lastReport is not null)
        {
            SolutionsStale = true;
        }
    }

    private DocumentSnapshot CaptureSnapshot() =>
        new(_document.Clone(), _selectedPiece?.Id);

    private bool SameAsBaseline() =>
        _baseline is not null &&
        string.Equals(PuzzleYaml.Save(_baseline.Document), PuzzleYaml.Save(_document), StringComparison.Ordinal);

    private void ApplySnapshot(DocumentSnapshot snapshot)
    {
        _document.Changed -= OnDocumentChanged;
        _document = snapshot.Document;
        _document.Changed += OnDocumentChanged;
        OnPropertyChanged(nameof(Document));

        SelectedPiece = _document.Pieces.FirstOrDefault(piece => piece.Id == snapshot.SelectedPieceId)
                        ?? _document.Pieces.FirstOrDefault();
        RefreshDocumentState(markDirty: true);
        ClearReport();
        _baseline = CaptureSnapshot();
        PersistRecovery();
        OnPropertyChanged(nameof(BoardWidthValue));
        OnPropertyChanged(nameof(BoardHeightValue));
        UpdateCommandStates();
    }

    private void PersistRecovery()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.ConfigDirectory);
            File.WriteAllText(RecoveryFile, PuzzleYaml.Save(_document));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recovery is best-effort; the editor remains usable if it cannot write.
        }
    }

    private void ClearRecovery()
    {
        try
        {
            if (File.Exists(RecoveryFile))
            {
                File.Delete(RecoveryFile);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore cleanup failures; they do not block editing.
        }
    }

    private void RefreshDocumentState(bool markDirty)
    {
        _suppressDocumentEvents = true;
        try
        {
            RecolorPieces();
            BoardSurface = _document.Board.Surface;
            RefreshPieceSurface();
            RefreshValidation();
            OnPropertyChanged(nameof(BoardSummary));
            if (markDirty)
            {
                IsDirty = true;
            }
        }
        finally
        {
            _suppressDocumentEvents = false;
        }

        UpdateCommandStates();
    }

    private void RecolorPieces()
    {
        for (var i = 0; i < _document.Pieces.Count; i++)
        {
            _document.Pieces[i].ColorIndex = i + 1;
        }

        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(SelectedPieceId));
        OnPropertyChanged(nameof(PieceCountValue));
    }

    private void RefreshPieceSurface()
    {
        PieceSurface = _selectedPiece is null
            ? null
            : _selectedPiece.Shape.BuildSurface(Palette.All, _selectedPiece.ColorIndex, 0);
        OnPropertyChanged(nameof(ShapeWidthValue));
        OnPropertyChanged(nameof(ShapeHeightValue));
    }

    private void RefreshValidation()
    {
        _document.Validate(out var errors, out var warnings);
        if (errors.Count > 0)
        {
            ValidationValid = false;
            ValidationText = string.Join(Environment.NewLine, errors.Take(3));
            if (errors.Count > 3)
            {
                ValidationText += $"{Environment.NewLine}{string.Format(Loc["status.moreErrors"], errors.Count - 3)}";
            }
        }
        else if (warnings.Count > 0)
        {
            ValidationValid = true;
            ValidationText = string.Join(Environment.NewLine, warnings.Take(2));
        }
        else
        {
            ValidationValid = true;
            ValidationText = Loc["solver.validationFallback"];
        }
    }

    private void ApplyReport(SolveOutcome outcome)
    {
        _lastReport = outcome.Report;
        _lastJson = outcome.RawJson;
        SolutionCount = outcome.Report?.Solutions.Count ?? 0;
        SolutionsStale = _revision != _solveRevision;
        RebuildStatistics(outcome);
        SelectedSolutionIndex = _solutionCount > 0 ? 1 : 0;
        UpdateSolutionView();
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(EmptyHintText));
        OnPropertyChanged(nameof(EmptyHintVisible));
        UpdateCommandStates();
    }

    private void ClearReport()
    {
        _lastReport = null;
        _lastJson = string.Empty;
        SolutionCount = 0;
        SelectedSolutionIndex = 0;
        SolutionSurface = null;
        Placements.Clear();
        StatLine = string.Empty;
        MultiplicityText = string.Empty;
        SolutionsStale = false;
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(EmptyHintText));
        OnPropertyChanged(nameof(EmptyHintVisible));
    }

    private void RebuildStatistics(SolveOutcome outcome)
    {
        var report = outcome.Report;
        if (report is null)
        {
            StatLine = string.Format(Loc["stat.solvedIn"], outcome.Duration.TotalSeconds);
            return;
        }

        var stats = report.Stats;
        var parts = new List<string>
        {
            stats.Solutions == 1 ? Loc["status.solutionsCountOne"] : string.Format(Loc["status.solutionsCount"], stats.Solutions),
            string.Format(Loc["stat.rawLayouts"], stats.RawSolutions),
            string.Format(Loc["stat.nodes"], stats.Nodes),
            $"{stats.ElapsedMs / 1000:0.00} s",
        };
        if (stats.Truncated)
        {
            parts.Add(Loc["stat.truncated"]);
        }

        if (stats.SymmetryGroup > 1)
        {
            parts.Add(string.Format(Loc["stat.symmetries"], stats.SymmetryGroup));
        }

        StatLine = string.Join("  ·  ", parts);
    }

    private void UpdateSolutionView()
    {
        Placements.Clear();
        var report = _lastReport;
        if (report is null || _solutionCount == 0)
        {
            SolutionSurface = null;
            MultiplicityText = string.Empty;
            return;
        }

        var solution = report.Solutions.ElementAtOrDefault(_selectedSolutionIndex - 1);
        if (solution is null)
        {
            SolutionSurface = null;
            MultiplicityText = string.Empty;
            return;
        }

        var width = report.Puzzle.Board.Width;
        var height = report.Puzzle.Board.Height;
        if (width <= 0 || height <= 0)
        {
            width = _document.Board.Width;
            height = _document.Board.Height;
        }

        var cells = new int[width * height];
        var labels = new string[width * height];
        foreach (var placement in solution.Placements)
        {
            var color = Placements.Count + 1;
            var label = color.ToString(CultureInfo.InvariantCulture);
            foreach (var cell in placement.Cells)
            {
                if (cell.Length < 2)
                {
                    continue;
                }

                var x = cell[0];
                var y = cell[1];
                if (x < 0 || x >= width || y < 0 || y >= height)
                {
                    continue;
                }

                var index = y * width + x;
                cells[index] = color;
                labels[index] = label;
            }

            Placements.Add(new PlacementRow(
                $"{Placements.Count + 1}.  {placement.Piece}   ·   " +
                string.Format(Loc["placement.cells"], placement.Cells.Length) +
                $"   ·   {placement.RotationDegrees}°" +
                (placement.Flipped ? $"   ·   {Loc["placement.flipped"]}" : string.Empty),
                new SolidColorBrush(Palette.ColorFor(color))));
        }

        SolutionSurface = new GridData(width, height, cells, labels, Palette.All);
        MultiplicityText = solution.SymmetryMultiplicity > 1
            ? string.Format(Loc["placement.represents"], solution.SymmetryMultiplicity)
            : Loc["placement.unique"];
    }

    private void UpdateSolverStatus()
    {
        OnPropertyChanged(nameof(SolverStatusText));
        OnPropertyChanged(nameof(SolverFound));
    }

    private void UpdateTitle()
    {
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void UpdateCommandStates()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        AddPieceCommand.NotifyCanExecuteChanged();
        RemovePieceCommand.NotifyCanExecuteChanged();
        DuplicatePieceCommand.NotifyCanExecuteChanged();
        RotatePieceLeftCommand.NotifyCanExecuteChanged();
        RotatePieceRightCommand.NotifyCanExecuteChanged();
        MirrorPieceCommand.NotifyCanExecuteChanged();
        TrimPieceCommand.NotifyCanExecuteChanged();
        ClearPieceCommand.NotifyCanExecuteChanged();
        SolveCommand.NotifyCanExecuteChanged();
        CancelSolveCommand.NotifyCanExecuteChanged();
        ValidateWithSolverCommand.NotifyCanExecuteChanged();
        PreviousSolutionCommand.NotifyCanExecuteChanged();
        NextSolutionCommand.NotifyCanExecuteChanged();
        FirstSolutionCommand.NotifyCanExecuteChanged();
        LastSolutionCommand.NotifyCanExecuteChanged();
    }
}
