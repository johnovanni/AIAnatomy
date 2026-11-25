using AIAnatomy.Models.Search;

namespace AIAnatomy.Models.Tests.Search;

public class MinimaxTests
{
    // A separate recursion that does not go through Minimax.
    private static void Count(Board b, ref long positions, ref long finished)
    {
        positions++;
        if (b.IsOver) { finished++; return; }
        foreach (var c in b.EmptyCells) Count(b.Play(c), ref positions, ref finished);
    }

    [Fact]
    public void TheFullGameTree_Has549946Positions_And255168FinishedGames()
    {
        long positions = 0, finished = 0;
        Count(Board.Empty, ref positions, ref finished);
        Assert.Equal(549946, positions);
        Assert.Equal(255168, finished);
    }

    [Fact]
    public void Search_FromTheEmptyBoard_LooksAtEveryPosition_AndIsADraw()
    {
        var result = Minimax.Search(Board.Empty);
        Assert.Equal(549946, result.PositionsLookedAt);
        Assert.Equal(9, result.Moves.Count);
        Assert.All(result.Moves, m => Assert.Equal(Outcome.Draw, m.Outcome));
        Assert.Equal(1 + result.Moves.Sum(m => m.PositionsLookedAt), result.PositionsLookedAt);
        foreach (var m in result.Moves)
        {
            long positions = 0, finished = 0;
            Count(Board.Empty.Play(m.Cell), ref positions, ref finished);
            Assert.Equal(positions, m.PositionsLookedAt);
        }
    }

    [Fact]
    public void Search_TakesAnImmediateWin()
    {
        var result = Minimax.Search(Board.Parse("XX.OO...."));
        Assert.Equal(2, result.BestCell);
        var best = result.Moves.Single(m => m.Cell == 2);
        Assert.Equal(Outcome.Win, best.Outcome);
        Assert.Equal(1, best.Plies);
    }

    [Fact]
    public void Search_BlocksAThreat()
    {
        var result = Minimax.Search(Board.Parse("XX..O...."));
        Assert.Equal(Mark.O, Board.Parse("XX..O....").ToMove);
        Assert.Equal(2, result.BestCell);
        Assert.All(result.Moves.Where(m => m.Cell != 2), m => Assert.Equal(Outcome.Loss, m.Outcome));
    }

    [Theory]
    [InlineData(Mark.X)]
    [InlineData(Mark.O)]
    public void Search_NeverLoses_AsXOrAsO(Mark searcher)
    {
        var chosen = new Dictionary<string, int>();
        int Choose(Board b)
        {
            var key = Key(b);
            if (!chosen.TryGetValue(key, out var cell)) chosen[key] = cell = Minimax.Search(b).BestCell;
            return cell;
        }
        long games = 0;
        void Play(Board b)
        {
            if (b.IsOver)
            {
                games++;
                Assert.True(b.Winner == Mark.None || b.Winner == searcher, "the search lost: " + Key(b));
                return;
            }
            if (b.ToMove == searcher) Play(b.Play(Choose(b)));
            else foreach (var c in b.EmptyCells) Play(b.Play(c));
        }
        Play(Board.Empty);
        Assert.True(games > 0);
    }

    private static string Key(Board b) => new string(Enumerable.Range(0, 9).Select(i => b[i] switch { Mark.X => 'X', Mark.O => 'O', _ => '.' }).ToArray());

    [Fact]
    public void Search_IsCancellable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Minimax.Search(Board.Empty, cts.Token));
    }

    [Fact]
    public void Board_RejectsImpossiblePositions_AndTakenCells()
    {
        Assert.Throws<ArgumentException>(() => Board.Parse("OO......."));      // O has more marks
        Assert.Throws<ArgumentException>(() => Board.Parse("XXX......."));     // X has two more
        Assert.Throws<ArgumentException>(() => Board.Parse("XXXOO.OO."));      // X has three, O has four
        Assert.Throws<ArgumentException>(() => Board.Parse("XXXOOO..."));      // both have lines
        Assert.Throws<ArgumentException>(() => Board.Parse("XX.OO....."));  // wrong length
        Assert.Throws<ArgumentException>(() => Board.Parse("XX.OO...Z"));      // bad character
    }

    [Fact]
    public void Board_PlayRejectsTakenCells_AndFinishedGames()
    {
        var b = Board.Empty.Play(4);
        Assert.Throws<InvalidOperationException>(() => b.Play(4));
        var won = Board.Parse("XX.OO....").Play(2);
        Assert.True(won.IsOver);
        Assert.Equal(Mark.X, won.Winner);
        Assert.Equal(new[] { 0, 1, 2 }, won.WinningLine);
        Assert.Throws<InvalidOperationException>(() => won.Play(5));
        Assert.Throws<InvalidOperationException>(() => Minimax.Search(won));
    }

    [Fact]
    public void Search_AmongWins_PrefersTheFastestEvenFromAHigherCell()
    {
        // "XOO.X...." X has 0 and 4, O has 1 and 2; X to move. Cell 8 wins at once (0-4-8): Win, 1 ply.
        // Cell 3 makes two threats, 5 (3-4-5) and 6 (0-3-6), which O cannot both stop, so X wins on its
        // next move: Win in 3 plies (X3, O, X). Cell 6 likewise threatens 3 (0-3-6) and 8 (0-4-8): 3 plies.
        // Cells 3 and 6 are lower than 8 but slower, so the faster cell 8 must be chosen.
        var result = Minimax.Search(Board.Parse("XOO.X...."));
        Assert.Equal(8, result.BestCell);
        var eight = result.Moves.Single(m => m.Cell == 8);
        Assert.Equal(Outcome.Win, eight.Outcome);
        Assert.Equal(1, eight.Plies);
        var three = result.Moves.Single(m => m.Cell == 3);
        Assert.Equal(Outcome.Win, three.Outcome);
        Assert.Equal(3, three.Plies); // a fork: X3, any O reply, X wins
        var six = result.Moves.Single(m => m.Cell == 6);
        Assert.Equal(Outcome.Win, six.Outcome);
        Assert.Equal(3, six.Plies);
    }

    [Fact]
    public void Search_AmongLosses_PrefersTheOneThatHoldsOutLongest()
    {
        // "XOOX...X." X0 X3 X7, O1 O2; O to move. X threatens 6 (0-3-6).
        // Cells 4, 5, 8 ignore it: X6 wins next move, a Loss in 2 plies.
        // Cell 6 blocks, but then X4 makes two threats (3-4-5 and 0-4-8) and wins: a Loss in 4 plies.
        // Cell 4 is the lowest cell yet loses sooner, so cell 6 must be chosen.
        var result = Minimax.Search(Board.Parse("XOOX...X."));
        Assert.All(result.Moves, m => Assert.Equal(Outcome.Loss, m.Outcome));
        Assert.Equal(2, result.Moves.Single(m => m.Cell == 4).Plies);
        Assert.Equal(4, result.Moves.Single(m => m.Cell == 6).Plies);
        Assert.Equal(6, result.BestCell);
    }

    [Fact]
    public void Search_ADrawnPosition_IsADraw()
    {
        // "XOXXOOOX." X has 0 2 3 7, O has 1 4 5 6; X plays the last cell 8 and no line is made: Draw, 1 ply.
        var result = Minimax.Search(Board.Parse("XOXXOOOX."));
        var only = Assert.Single(result.Moves);
        Assert.Equal(8, only.Cell);
        Assert.Equal(Outcome.Draw, only.Outcome);
        Assert.Equal(1, only.Plies);
        Assert.Equal(2, result.PositionsLookedAt); // the root and the full board
    }

    [Fact]
    public void Board_Parse_RejectsAWinnerWhoWasNotTheLastToMove()
    {
        Assert.Throws<ArgumentException>(() => Board.Parse("XXXOO.O.."));  // X has a line but the counts are equal
        Assert.Throws<ArgumentException>(() => Board.Parse("XX.OOOX.X"));  // O has a line but X has one more mark
        Assert.Throws<ArgumentException>(() => Board.Parse(null!));
    }

    [Fact]
    public void Board_ExposedCollections_CannotBeChangedByACaller()
    {
        var won = Board.Parse("XX.OO....").Play(2);
        Assert.False(won.WinningLine is int[]);
        Assert.Equal(new[] { 0, 1, 2 }, won.WinningLine);
        Assert.False(Board.Empty.EmptyCells is int[]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)Board.Empty.EmptyCells)[0] = 5);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)won.WinningLine!)[0] = 5);
        Assert.Equal(new[] { 0, 1, 2 }, Board.Parse("XX.OO....").Play(2).WinningLine);
    }
}
