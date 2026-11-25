using System.Collections.ObjectModel;
using System.Globalization;
using AIAnatomy.Models.Search;
using AIAnatomy.Presentation.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>One square of the board as the view shows it: its mark, and whether it is part of the winning line.</summary>
public sealed record BoardCell(int Index, Mark Mark, bool IsInWinningLine)
{
    /// <summary>"X", "O" or "" for an empty square.</summary>
    public string Text => Mark switch { Mark.X => "X", Mark.O => "O", _ => "" };

    /// <summary>What a screen reader says: "centre, empty", "top left, O", "top right, O, in the winning line".</summary>
    public string AutomationName =>
        $"{SearchLabViewModel.CellName(Index)}, {(Mark == Mark.None ? "empty" : Text)}{(IsInWinningLine ? ", in the winning line" : "")}";
}

/// <summary>
/// One move in the look-ahead tree. Depth 1 is one of the options the computer weighed for its latest move; depth 2 is one
/// of your possible replies to it; depth 3 the computer's answer; depth 4 your reply again. <see cref="Outcome"/> is always
/// from the computer's point of view, whoever makes the move (ruling B): a reply's score comes back from the search for
/// the player making it and is turned round here when that player is you.
/// </summary>
public sealed partial class LookAheadNode : ObservableObject
{
    private readonly Board _after;
    private readonly Mark _computer;
    private readonly Func<Board, CancellationToken, SearchResult> _search;
    private bool _isExpanded, _filled;

    internal LookAheadNode(int cell, Outcome outcome, long positionsLookedAt, bool isChosen, int depth, Board after, Mark computer,
        Func<Board, CancellationToken, SearchResult> search)
    {
        Cell = cell;
        Outcome = outcome;
        PositionsLookedAt = positionsLookedAt;
        IsChosen = isChosen;
        Depth = depth;
        _after = after;
        _computer = computer;
        _search = search;
    }

    public int Cell { get; }

    /// <summary>From the computer's point of view.</summary>
    public Outcome Outcome { get; }

    /// <summary>How many positions the search looked at below this move, the position after the move included.</summary>
    public long PositionsLookedAt { get; }

    /// <summary>True for the option the computer played (depth 1 only).</summary>
    public bool IsChosen { get; }

    /// <summary>1 = the computer's options, 2 = your replies, 3 = the computer's answers, 4 = your replies.</summary>
    public int Depth { get; }

    /// <summary>True for the computer's moves (depths 1 and 3), false for your replies.</summary>
    public bool IsComputerMove => Depth % 2 == 1;

    /// <summary>
    /// "Centre: draw (7,331 positions), chosen" for one of the computer's options (with "chosen" in words on the one it
    /// played); "Top left: computer loses" deeper down, where the count is not part of the computer's own search.
    /// </summary>
    public string Label
    {
        get
        {
            string text = $"{Capitalised(SearchLabViewModel.CellName(Cell))}: {SearchLabViewModel.OutcomeWords(Outcome)}";
            if (Depth == 1)
                text += $" ({PositionsLookedAt.ToString("N0", CultureInfo.InvariantCulture)} {(PositionsLookedAt == 1 ? "position" : "positions")})";
            return IsChosen ? text + ", chosen" : text;
        }
    }

    /// <summary>The label with whose move it is, for screen readers and the tooltip.</summary>
    public string AutomationName => $"{Label}; {(IsComputerMove ? "a move the computer could make" : "a reply you could make")}";

    /// <summary>False at depth 4 and for a move that ends the game: there is nothing beneath it.</summary>
    public bool CanExpand => Depth < SearchLabViewModel.MaxDepth && !_after.IsOver;

    /// <summary>
    /// Expanding fills <see cref="Children"/> the first time, by searching the position after this move (small, so it is
    /// done at once); later expansions reuse them. A node that cannot expand stays closed, and so does one whose search
    /// fails (the failure is reported in the background, and the next try searches again).
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value && !CanExpand) value = false;
            if (value && !_filled && !TryFill()) value = false;
            SetProperty(ref _isExpanded, value);
        }
    }

    /// <summary>The replies to this move, each scored from the computer's point of view; empty until first expanded.</summary>
    public ObservableCollection<LookAheadNode> Children { get; } = new();

    private bool TryFill()
    {
        SearchResult result;
        try
        {
            result = _search(_after, CancellationToken.None);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            ErrorLog.ReportBackground(e);
            return false;
        }
        bool computerToMove = _after.ToMove == _computer;
        foreach (var move in result.Moves)
        {
            var outcome = computerToMove ? move.Outcome : (Outcome)(-(int)move.Outcome);
            Children.Add(new LookAheadNode(move.Cell, outcome, move.PositionsLookedAt, false, Depth + 1, _after.Play(move.Cell), _computer, _search));
        }
        _filled = true;   // only after a whole, successful fill
        return true;
    }

    private static string Capitalised(string s) => char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>
/// Lesson 4.3's lab: tic-tac-toe against plain minimax. You play X (you go first) or O (the computer goes first). After
/// each computer move the look-ahead lists the options it weighed with their scores, and the count says how many positions
/// its search looked at. Screens: "board" and "lookahead".
/// </summary>
/// <remarks>
/// The computer's search runs on <see cref="Task.Run(Action)"/> with this lab's cancellation token. A new game, Reset and
/// leaving the lab end the generation and cancel the token; since the search only looks at its token every 4,096
/// positions, a search can still come back afterwards, so its result is dropped unless its generation is still current.
/// </remarks>
public sealed partial class SearchLabViewModel : LabViewModel
{
    public const string Id = "SearchLab";

    /// <summary>The look-ahead opens at most this many moves deep.</summary>
    public const int MaxDepth = 4;

    private const string BoardScreen = "board", LookAheadScreen = "lookahead";

    /// <summary>The status after the computer's search failed (the failure itself goes to the error log).</summary>
    public const string FailedText = "The computer could not choose its move. Start a new game.";

    private static readonly string[] CellNames =
        ["top left", "top middle", "top right", "middle left", "centre", "middle right", "bottom left", "bottom middle", "bottom right"];

    private readonly Func<Board, CancellationToken, SearchResult> _search;
    private CancellationTokenSource _cancel = new();
    private IDisposable? _run;
    private int _generation;
    private bool _failed;
    private Task _work = Task.CompletedTask;

    /// <param name="search">The search to use (a test can hold it); <see cref="Minimax.Search"/> when not given.</param>
    public SearchLabViewModel(Func<Board, CancellationToken, SearchResult>? search = null)
    {
        _search = search ?? Minimax.Search;
        Refresh();   // also reports the baselines
    }

    public override string LabId => Id;

    public override Task WorkCompleted => _work;

    [ObservableProperty] private Board board = Board.Empty;
    [ObservableProperty] private Mark human = Mark.X;
    [ObservableProperty] private bool isThinking;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private string countText = "";
    [ObservableProperty] private IReadOnlyList<BoardCell> cells = [];

    /// <summary>The computer's mark: the other one.</summary>
    public Mark Computer => Human == Mark.X ? Mark.O : Mark.X;

    /// <summary>The options the computer weighed for its latest move, in cell order; empty before its first move.</summary>
    public ObservableCollection<LookAheadNode> LookAhead { get; } = new();

    /// <summary>"top left" … "bottom right".</summary>
    public static string CellName(int cell) => CellNames[cell];

    /// <summary>"computer wins", "draw" or "computer loses".</summary>
    public static string OutcomeWords(Outcome outcome) => outcome switch
    {
        Outcome.Win => "computer wins",
        Outcome.Loss => "computer loses",
        _ => "draw",
    };

    /// <summary>"top row", "left column", "diagonal from top left to bottom right" …</summary>
    public static string LineName(IReadOnlyList<int> line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return (line[0], line[1], line[2]) switch
        {
            (0, 1, 2) => "top row",
            (3, 4, 5) => "middle row",
            (6, 7, 8) => "bottom row",
            (0, 3, 6) => "left column",
            (1, 4, 7) => "middle column",
            (2, 5, 8) => "right column",
            (0, 4, 8) => "diagonal from top left to bottom right",
            (2, 4, 6) => "diagonal from top right to bottom left",
            _ => throw new ArgumentException("That is not a line of the board.", nameof(line)),
        };
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task NewGameAsX() => NewGameAsync(Mark.X);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task NewGameAsO() => NewGameAsync(Mark.O);

    /// <summary>Starts a new game with you as <paramref name="human"/>; with O the computer opens. Any search under way is
    /// cancelled and its result thrown away. The board and look-ahead screens light once, after the opening move if any.</summary>
    public Task NewGameAsync(Mark human)
    {
        if (human is not (Mark.X or Mark.O)) throw new ArgumentException("You play X or O.", nameof(human));
        StopSearch();
        _run = BeginRun();
        Human = human;
        Board = Board.Empty;
        LookAhead.Clear();
        CountText = "";
        Refresh();
        return _work = ComputerTurnAsync();
    }

    /// <summary>Plays your mark in <paramref name="cell"/> and lets the computer reply. Ignored while the computer is
    /// thinking, when the cell is taken or not on the board, and when the game is over.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    public Task PlayAsync(int cell)
    {
        if (IsThinking || Board.IsOver || Board.ToMove != Human || cell is < 0 or > 8 || Board[cell] != Mark.None)
            return Task.CompletedTask;
        EndRun();
        _run = BeginRun();   // your mark and the computer's reply light the screens once
        Board = Board.Play(cell);
        Refresh();
        return _work = ComputerTurnAsync();
    }

    public override void Reset() => _ = NewGameAsync(Mark.X);   // you go first, so nothing is searched

    public override void Deactivate() => StopSearch();

    private async Task ComputerTurnAsync()
    {
        if (Board.IsOver || Board.ToMove == Human)
        {
            EndRun();
            return;
        }
        int generation = _generation;
        var token = _cancel.Token;
        var position = Board;
        IsThinking = true;
        Refresh();
        SearchResult result;
        try
        {
            result = await Task.Run(() => _search(position, token), token);
        }
        catch (OperationCanceledException) when (generation != _generation)
        {
            return;   // cancelled by a new game, Reset or leaving the lab
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Nobody awaits a button's command, so the failure is logged and shown in words, and the lab stops thinking.
            if (generation != _generation) return;
            ErrorLog.ReportBackground(e);
            _failed = true;
            IsThinking = false;
            Refresh();
            EndRun();
            return;
        }
        if (generation != _generation) return;   // a late result from a search that was cancelled: thrown away

        var computer = Computer;
        LookAhead.Clear();
        foreach (var move in result.Moves)
            LookAhead.Add(new LookAheadNode(move.Cell, move.Outcome, move.PositionsLookedAt, move.Cell == result.BestCell, 1,
                position.Play(move.Cell), computer, _search));
        CountText = $"The computer looked at {result.PositionsLookedAt.ToString("N0", CultureInfo.InvariantCulture)} positions to choose its move.";
        Board = position.Play(result.BestCell);
        IsThinking = false;
        Refresh();
        EndRun();
    }

    /// <summary>Ends the generation: a search under way is cancelled and whatever it returns is thrown away.</summary>
    private void StopSearch()
    {
        _generation++;
        _failed = false;
        _cancel.Cancel();
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        IsThinking = false;
        EndRun();
        Refresh();
    }

    private void EndRun()
    {
        var run = _run;
        _run = null;
        run?.Dispose();
    }

    private void Refresh()
    {
        var line = Board.WinningLine;
        Cells = Enumerable.Range(0, 9).Select(i => new BoardCell(i, Board[i], line?.Contains(i) ?? false)).ToList();
        StatusText = IsThinking ? "The computer is thinking…"
            : _failed ? FailedText
            : Board.Winner != Mark.None ? $"{Board.Winner} wins: {LineName(line!)}."
            : Board.IsOver ? "A draw."
            : $"Your move (you are {Human}).";
        ReportScreen(BoardScreen, string.Concat(Cells.Select(c => c.Mark switch { Mark.X => 'X', Mark.O => 'O', _ => '.' })));
        ReportScreen(LookAheadScreen, new[] { CountText }.Concat(LookAhead.Select(n => n.Label)).ToArray());
    }

    partial void OnHumanChanged(Mark value) => OnPropertyChanged(nameof(Computer));
}
