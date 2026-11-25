using System.Diagnostics;
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
        var sw = Stopwatch.StartNew();
        var result = Minimax.Search(Board.Empty);
        sw.Stop();
        Assert.Equal(549946, result.PositionsLookedAt);
        Assert.Equal(9, result.Moves.Count);
        Assert.All(result.Moves, m => Assert.Equal(Outcome.Draw, m.Outcome));
        Assert.Equal(1 + result.Moves.Sum(m => m.PositionsLookedAt), result.PositionsLookedAt);
        Assert.True(sw.Elapsed.TotalSeconds < 2, $"took {sw.Elapsed}");
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
}
