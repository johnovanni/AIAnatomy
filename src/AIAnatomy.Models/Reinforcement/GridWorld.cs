namespace AIAnatomy.Models.Reinforcement;

/// <summary>One of the four moves. The order (Up, Right, Down, Left) is the fixed tie-break order for showing arrows.</summary>
public enum Move { Up, Right, Down, Left }

/// <summary>A cell of the grid; (0, 0) is the top left.</summary>
public readonly record struct GridCell(int Column, int Row);

/// <summary>A small maze: a rectangular grid with walls, a start cell and a goal cell.</summary>
public sealed class GridWorld
{
    private const int MinSize = 2;
    private const int MaxSize = 12;

    private readonly HashSet<GridCell> _walls;

    public GridWorld(int columns, int rows, IEnumerable<GridCell> walls, GridCell start, GridCell goal)
    {
        if (columns < MinSize || columns > MaxSize)
            throw new ArgumentException($"Columns must be {MinSize} to {MaxSize}.", nameof(columns));
        if (rows < MinSize || rows > MaxSize)
            throw new ArgumentException($"Rows must be {MinSize} to {MaxSize}.", nameof(rows));
        ArgumentNullException.ThrowIfNull(walls);

        Columns = columns;
        Rows = rows;
        _walls = [.. walls];

        foreach (GridCell wall in _walls)
            if (!OnGrid(wall))
                throw new ArgumentException($"Wall {wall} is off the grid.", nameof(walls));
        if (!OnGrid(start))
            throw new ArgumentException("The start cell is off the grid.", nameof(start));
        if (!OnGrid(goal))
            throw new ArgumentException("The goal cell is off the grid.", nameof(goal));
        if (start == goal)
            throw new ArgumentException("The start and the goal must be different cells.", nameof(goal));
        if (_walls.Contains(start))
            throw new ArgumentException("The start cell is a wall.", nameof(start));
        if (_walls.Contains(goal))
            throw new ArgumentException("The goal cell is a wall.", nameof(goal));

        Start = start;
        Goal = goal;
    }

    public int Columns { get; }
    public int Rows { get; }
    public GridCell Start { get; }
    public GridCell Goal { get; }
    public IReadOnlySet<GridCell> Walls => _walls;

    public bool IsWall(GridCell cell) => _walls.Contains(cell);

    /// <summary>The cell reached by the move. Off the grid or into a wall, the agent stays where it is.</summary>
    public GridCell Next(GridCell @from, Move move)
    {
        GridCell to = move switch
        {
            Move.Up => @from with { Row = from.Row - 1 },
            Move.Right => @from with { Column = from.Column + 1 },
            Move.Down => @from with { Row = from.Row + 1 },
            Move.Left => @from with { Column = from.Column - 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(move)),
        };
        return OnGrid(to) && !_walls.Contains(to) ? to : @from;
    }

    /// <summary>Whether the goal can be reached from the start (breadth-first search).</summary>
    public bool GoalReachable
    {
        get
        {
            var seen = new HashSet<GridCell> { Start };
            var queue = new Queue<GridCell>();
            queue.Enqueue(Start);
            while (queue.Count > 0)
            {
                GridCell cell = queue.Dequeue();
                if (cell == Goal) return true;
                foreach (Move move in Enum.GetValues<Move>())
                {
                    GridCell next = Next(cell, move);
                    if (seen.Add(next)) queue.Enqueue(next);
                }
            }
            return false;
        }
    }

    private bool OnGrid(GridCell cell) =>
        cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;
}
