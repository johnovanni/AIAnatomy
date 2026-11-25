namespace AIAnatomy.Models.Search;

/// <summary>A result from one player's point of view.</summary>
public enum Outcome { Loss = -1, Draw = 0, Win = 1 }

/// <summary>One move, scored for the player making it. <see cref="Plies"/> counts this move.</summary>
public sealed record MoveScore(int Cell, Outcome Outcome, int Plies, long PositionsLookedAt);

public sealed record SearchResult(int BestCell, IReadOnlyList<MoveScore> Moves, long PositionsLookedAt);

/// <summary>
/// Plain minimax on tic-tac-toe: no pruning, no caching, every line played to the end of the game.
/// Best move: a win before a draw before a loss; among wins the fewest plies, among losses the most;
/// then the lowest cell.
/// </summary>
public static class Minimax
{
    private const long CheckEvery = 4096;

    public static SearchResult Search(Board board, CancellationToken token = default)
    {
        if (board.IsOver) throw new InvalidOperationException("The game is over; there is nothing to search.");
        long count = 1; // the root
        var moves = new List<MoveScore>();
        foreach (var cell in board.EmptyCells)
        {
            long before = count;
            var (outcome, plies) = Evaluate(board.Play(cell), token, ref count);
            moves.Add(new MoveScore(cell, Flip(outcome), plies + 1, count - before));
        }
        return new SearchResult(Best(moves).Cell, moves, count);
    }

    private static Outcome Flip(Outcome o) => (Outcome)(-(int)o);

    private static MoveScore Best(List<MoveScore> moves)
    {
        var best = moves[0];
        for (int i = 1; i < moves.Count; i++)
            if (Better(moves[i], best)) best = moves[i];
        return best;
    }

    // Cells are tried in order, so on a full tie the earlier (lower) cell is kept.
    private static bool Better(MoveScore a, MoveScore b)
    {
        if (a.Outcome != b.Outcome) return a.Outcome > b.Outcome;
        if (a.Outcome == Outcome.Win) return a.Plies < b.Plies;
        if (a.Outcome == Outcome.Loss) return a.Plies > b.Plies;
        return false;
    }

    /// <summary>The value of a position for the player to move there, and the plies left with best play.</summary>
    private static (Outcome Outcome, int Plies) Evaluate(Board board, CancellationToken token, ref long count)
    {
        count++;
        if (count % CheckEvery == 0) token.ThrowIfCancellationRequested();
        if (board.IsOver) return (board.Winner == Mark.None ? Outcome.Draw : Outcome.Loss, 0);

        MoveScore? best = null;
        foreach (var cell in board.EmptyCells)
        {
            long before = count;
            var (outcome, plies) = Evaluate(board.Play(cell), token, ref count);
            var m = new MoveScore(cell, Flip(outcome), plies + 1, count - before);
            if (best is null || Better(m, best)) best = m;
        }
        return (best!.Outcome, best.Plies);
    }
}
