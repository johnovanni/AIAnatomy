namespace AIAnatomy.Models.Search;

public enum Mark { None, X, O }

/// <summary>An immutable tic-tac-toe board. Cells 0 to 8 run row by row from the top left; X always moves first.</summary>
public sealed class Board
{
    private static readonly int[][] Lines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 },
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 },
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },
    };

    private readonly Mark[] _cells;
    private readonly int _xCount, _oCount;

    public static Board Empty { get; } = new(new Mark[9]);

    private Board(Mark[] cells)
    {
        _cells = cells;
        foreach (var c in cells)
        {
            if (c == Mark.X) _xCount++;
            else if (c == Mark.O) _oCount++;
        }
        foreach (var line in Lines)
        {
            var m = cells[line[0]];
            if (m != Mark.None && cells[line[1]] == m && cells[line[2]] == m)
            {
                Winner = m;
                WinningLine = line;
                break;
            }
        }
        ToMove = _xCount == _oCount ? Mark.X : Mark.O;
        IsOver = Winner != Mark.None || _xCount + _oCount == 9;
        EmptyCells = IsOver ? Array.Empty<int>() : Enumerable.Range(0, 9).Where(i => cells[i] == Mark.None).ToArray();
    }

    /// <summary>Reads nine characters ('.', 'X', 'O'); whose turn it is follows from the counts.</summary>
    public static Board Parse(string nine)
    {
        if (nine is null || nine.Length != 9) throw new ArgumentException("A board is nine characters: '.', 'X' or 'O'.", nameof(nine));
        var cells = new Mark[9];
        for (int i = 0; i < 9; i++)
        {
            cells[i] = nine[i] switch
            {
                '.' => Mark.None,
                'X' => Mark.X,
                'O' => Mark.O,
                _ => throw new ArgumentException($"Unknown character '{nine[i]}'; use '.', 'X' or 'O'.", nameof(nine)),
            };
        }
        int x = cells.Count(c => c == Mark.X), o = cells.Count(c => c == Mark.O);
        if (x != o && x != o + 1) throw new ArgumentException("X moves first, so X has as many marks as O or one more.", nameof(nine));
        var board = new Board(cells);
        if (HasBothLines(cells)) throw new ArgumentException("Both players cannot have a line.", nameof(nine));
        if (board.Winner == Mark.X && x != o + 1) throw new ArgumentException("X has a line but O moved after it.", nameof(nine));
        if (board.Winner == Mark.O && x != o) throw new ArgumentException("O has a line but X moved after it.", nameof(nine));
        return board;
    }

    private static bool HasBothLines(Mark[] cells)
    {
        bool x = false, o = false;
        foreach (var l in Lines)
        {
            var m = cells[l[0]];
            if (m == Mark.None || cells[l[1]] != m || cells[l[2]] != m) continue;
            if (m == Mark.X) x = true; else o = true;
        }
        return x && o;
    }

    public Mark this[int cell] => _cells[cell];
    public Mark ToMove { get; }
    public Mark Winner { get; } = Mark.None;
    public IReadOnlyList<int>? WinningLine { get; }
    public bool IsOver { get; }
    public IReadOnlyList<int> EmptyCells { get; }

    public Board Play(int cell)
    {
        if (IsOver) throw new InvalidOperationException("The game is over.");
        if (cell < 0 || cell > 8 || _cells[cell] != Mark.None) throw new InvalidOperationException($"Cell {cell} is not free.");
        var next = (Mark[])_cells.Clone();
        next[cell] = ToMove;
        return new Board(next);
    }
}
