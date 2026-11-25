using System.Globalization;
using AIAnatomy.Models.Search;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class SearchLabTests
{
    private static string N(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    private static string Cells(Board b) => string.Concat(Enumerable.Range(0, 9).Select(i => b[i] switch { Mark.X => 'X', Mark.O => 'O', _ => '.' }));

    /// <summary>
    /// A search the test can hold: the first call (or every call, with <see cref="HoldEvery"/>) waits until
    /// <see cref="Release"/>, and it ignores the token, so a cancelled search still comes back with a (late) result.
    /// No timing is involved: the test decides when the search may finish.
    /// </summary>
    private sealed class HeldSearch
    {
        private readonly ManualResetEventSlim _gate = new(false), _started = new(false);
        private int _calls;
        public bool HoldEvery { get; init; }
        public CancellationToken LastToken { get; private set; }
        public int Calls => _calls;

        public SearchResult Search(Board board, CancellationToken token)
        {
            int call = Interlocked.Increment(ref _calls);
            LastToken = token;
            _started.Set();
            if (call == 1 || HoldEvery) _gate.Wait(TimeSpan.FromMinutes(1));   // the minute is only a guard against a hung test
            return Minimax.Search(board);
        }

        public void Release() => _gate.Set();

        /// <summary>Waits until the search has been called (it runs on another thread), so its token can be read.</summary>
        public void WaitUntilStarted() => Assert.True(_started.Wait(TimeSpan.FromMinutes(1)), "the search never started");
    }

    [Fact]
    public void Opens_AsANewGame_WithTheHumanAsX()
    {
        var lab = new SearchLabViewModel();
        Assert.Equal(SearchLabViewModel.Id, lab.LabId);
        Assert.Equal(Mark.X, lab.Human);
        Assert.Equal(".........", Cells(lab.Board));
        Assert.Empty(lab.LookAhead);
        Assert.False(lab.IsThinking);
        Assert.Equal("Your move (you are X).", lab.StatusText);
        Assert.Equal("", lab.CountText);
        Assert.Equal(
            ["top left, empty", "top middle, empty", "top right, empty", "middle left, empty", "centre, empty",
             "middle right, empty", "bottom left, empty", "bottom middle, empty", "bottom right, empty"],
            lab.Cells.Select(c => c.AutomationName));
        Assert.All(lab.Cells, c => Assert.Equal("", c.Text));
    }

    [Fact]
    public async Task NewGameAsO_TheComputerOpens_HavingLookedAt549946Positions()
    {
        var lab = new SearchLabViewModel();
        await lab.NewGameAsync(Mark.O);
        Assert.Equal(Mark.O, lab.Human);
        Assert.Equal("The computer looked at 549,946 positions to choose its move.", lab.CountText);
        Assert.Equal(9, lab.LookAhead.Count);
        Assert.All(lab.LookAhead, n => { Assert.Equal(Outcome.Draw, n.Outcome); Assert.Equal(1, n.Depth); });
        var chosen = Assert.Single(lab.LookAhead, n => n.IsChosen);
        Assert.Equal(0, chosen.Cell);   // every opening draws, so the tie-break takes the lowest cell
        Assert.Equal("X........", Cells(lab.Board));
        Assert.Equal($"Top left: draw ({N(chosen.PositionsLookedAt)} positions), chosen", chosen.Label);
        Assert.Equal($"Centre: draw ({N(lab.LookAhead[4].PositionsLookedAt)} positions)", lab.LookAhead[4].Label);
        Assert.Equal(549_946 - 1, lab.LookAhead.Sum(n => n.PositionsLookedAt));   // the options' counts plus the root
        Assert.Equal("Your move (you are O).", lab.StatusText);
        Assert.False(lab.IsThinking);
    }

    [Fact]
    public async Task AfterTheHumanPlaysTheCentre_TheComputerReplies_AndListsItsEightOptions()
    {
        var lab = new SearchLabViewModel();
        await lab.PlayAsync(4);
        Assert.Equal(8, lab.LookAhead.Count);
        Assert.Equal([0, 1, 2, 3, 5, 6, 7, 8], lab.LookAhead.Select(n => n.Cell));
        // Hand-worked: against X in the centre, O draws in a corner and loses on an edge.
        foreach (var n in lab.LookAhead)
            Assert.Equal(n.Cell is 0 or 2 or 6 or 8 ? Outcome.Draw : Outcome.Loss, n.Outcome);
        var chosen = Assert.Single(lab.LookAhead, n => n.IsChosen);
        Assert.Equal(0, chosen.Cell);
        Assert.Equal("O...X....", Cells(lab.Board));
        Assert.Equal(Mark.O, lab.Cells[0].Mark);
        Assert.Equal("top left, O", lab.Cells[0].AutomationName);
        Assert.Equal("O", lab.Cells[0].Text);
        Assert.Equal("centre, X", lab.Cells[4].AutomationName);
        long count = Minimax.Search(Board.Parse("....X....")).PositionsLookedAt;
        Assert.Equal($"The computer looked at {N(count)} positions to choose its move.", lab.CountText);
        Assert.Equal($"Top middle: computer loses ({N(lab.LookAhead[1].PositionsLookedAt)} positions)", lab.LookAhead[1].Label);
        Assert.EndsWith(", chosen", chosen.Label);
        Assert.Equal("Your move (you are X).", lab.StatusText);
    }

    /// <summary>Every position from <paramref name="b"/> to the ends of the game, <paramref name="b"/> included, counted
    /// without <see cref="Minimax"/>: the same counting rule, worked independently.</summary>
    private static long CountPositions(Board b) => 1 + (b.IsOver ? 0 : b.EmptyCells.Sum(c => CountPositions(b.Play(c))));

    [Fact]
    public async Task AgainstACornerOpening_TheCentreIsADrawAfter7332Positions()
    {
        var lab = new SearchLabViewModel();
        await lab.PlayAsync(0);
        var centre = Assert.Single(lab.LookAhead, n => n.Cell == 4);
        Assert.Equal(7_332, CountPositions(Board.Parse("X...O....")));   // the position after the move and all below it
        Assert.Equal("Centre: draw (7,332 positions), chosen", centre.Label);
        Assert.All(lab.LookAhead.Where(n => n.Cell != 4), n => Assert.Equal(Outcome.Loss, n.Outcome));   // only the centre holds the draw
    }

    [Fact]
    public async Task Expanding_FillsTheReplies_ScoredFromTheComputersView()
    {
        var lab = new SearchLabViewModel();
        await lab.PlayAsync(4);
        var edge = lab.LookAhead.Single(n => n.Cell == 1);   // O on the top middle: computer loses
        Assert.Empty(edge.Children);
        Assert.True(edge.CanExpand);
        edge.IsExpanded = true;
        Assert.True(edge.IsExpanded);
        Assert.Equal([0, 2, 3, 5, 6, 7, 8], edge.Children.Select(c => c.Cell));
        Assert.All(edge.Children, c => { Assert.Equal(2, c.Depth); Assert.False(c.IsChosen); });
        // The replies are the human's: the human picks the one worst for the computer, which is the option's own score.
        Assert.Equal(edge.Outcome, edge.Children.Min(c => c.Outcome));
        // Hand-worked: X replies bottom left, threatening the diagonal; O must block top right; X top left then makes two lines.
        var bottomLeft = edge.Children.Single(c => c.Cell == 6);
        Assert.Equal(Outcome.Loss, bottomLeft.Outcome);
        Assert.Equal("Bottom left: computer loses", bottomLeft.Label);

        // One level down the computer moves again: it picks the best for itself.
        var reply = edge.Children.First(c => c.CanExpand);
        reply.IsExpanded = true;
        Assert.NotEmpty(reply.Children);
        Assert.All(reply.Children, c => Assert.Equal(3, c.Depth));
        Assert.Equal(reply.Outcome, reply.Children.Max(c => c.Outcome));

        // Expanding again searches nothing new: the same children stay.
        var first = edge.Children.ToArray();
        edge.IsExpanded = false;
        edge.IsExpanded = true;
        Assert.Equal(first, edge.Children.ToArray());
    }

    [Fact]
    public async Task ADrawingCorner_HasADrawingBestReplyFromTheHuman()
    {
        var lab = new SearchLabViewModel();
        await lab.PlayAsync(4);
        var corner = lab.LookAhead.Single(n => n.Cell == 0);
        corner.IsExpanded = true;
        Assert.Equal(Outcome.Draw, corner.Children.Min(c => c.Outcome));
        Assert.All(corner.Children, c => Assert.NotEqual(Outcome.Loss, c.Outcome));   // no reply of yours beats O there
        Assert.Equal("Top middle: draw", corner.Children.Single(c => c.Cell == 1).Label);
    }

    [Fact]
    public async Task Depth4Nodes_CannotExpand()
    {
        var lab = new SearchLabViewModel();
        await lab.NewGameAsync(Mark.O);
        var node = lab.LookAhead[0];
        for (int depth = 1; depth < SearchLabViewModel.MaxDepth; depth++)
        {
            Assert.True(node.CanExpand);
            node.IsExpanded = true;
            node = node.Children.First(c => c.CanExpand || c.Depth == SearchLabViewModel.MaxDepth);
        }
        Assert.Equal(4, node.Depth);
        Assert.False(node.CanExpand);
        node.IsExpanded = true;
        Assert.False(node.IsExpanded);
        Assert.Empty(node.Children);
    }

    [Fact]
    public async Task AWinningOption_EndsTheGame_SoItCannotExpand()
    {
        var lab = new SearchLabViewModel();
        foreach (int cell in new[] { 0, 1, 3 }) await lab.PlayAsync(cell);   // see Losing_TheStatusNamesTheWinnerAndTheLine
        var win = Assert.Single(lab.LookAhead, n => n.IsChosen);
        Assert.Equal(Outcome.Win, win.Outcome);
        Assert.Equal("Bottom left: computer wins (1 position), chosen", win.Label);
        Assert.False(win.CanExpand);
        win.IsExpanded = true;
        Assert.Empty(win.Children);
    }

    [Fact]
    public async Task Losing_TheStatusNamesTheWinnerAndTheLine()
    {
        // The human (X) always takes the lowest free cell: X top left, O centre, X top middle, O top right (blocking),
        // X middle left, and O completes the diagonal from top right to bottom left.
        var lab = new SearchLabViewModel();
        while (!lab.Board.IsOver) await lab.PlayAsync(lab.Board.EmptyCells[0]);
        Assert.Equal("XXOXO.O..", Cells(lab.Board));
        Assert.Equal(Mark.O, lab.Board.Winner);
        Assert.Equal("O wins: diagonal from top right to bottom left.", lab.StatusText);
        Assert.Equal([2, 4, 6], lab.Cells.Where(c => c.IsInWinningLine).Select(c => c.Index));
        Assert.Equal("top right, O, in the winning line", lab.Cells[2].AutomationName);
    }

    [Fact]
    public async Task BestPlayOnBothSides_IsADraw()
    {
        var lab = new SearchLabViewModel();
        while (!lab.Board.IsOver) await lab.PlayAsync(Minimax.Search(lab.Board).BestCell);
        Assert.Equal(Mark.None, lab.Board.Winner);
        Assert.Equal("A draw.", lab.StatusText);
        Assert.DoesNotContain(lab.Cells, c => c.IsInWinningLine);
    }

    [Fact]
    public async Task Play_IsIgnored_WhileThinking_OnATakenCell_AndAfterTheGameEnds()
    {
        var held = new HeldSearch();
        var lab = new SearchLabViewModel(held.Search);
        var first = lab.PlayAsync(4);
        Assert.True(lab.IsThinking);
        Assert.Equal("The computer is thinking…", lab.StatusText);
        await lab.PlayAsync(0);   // while thinking
        Assert.Equal("....X....", Cells(lab.Board));
        held.Release();
        await first;
        Assert.False(lab.IsThinking);
        Assert.Equal("O...X....", Cells(lab.Board));
        Assert.Equal(1, held.Calls);

        var options = lab.LookAhead.ToArray();
        await lab.PlayAsync(4);   // taken by X
        await lab.PlayAsync(0);   // taken by O
        await lab.PlayAsync(9);   // no such cell
        Assert.Equal("O...X....", Cells(lab.Board));
        Assert.Equal(options, lab.LookAhead.ToArray());
        Assert.Equal(1, held.Calls);

        var over = new SearchLabViewModel();
        while (!over.Board.IsOver) await over.PlayAsync(over.Board.EmptyCells[0]);
        string end = Cells(over.Board), status = over.StatusText;
        await over.PlayAsync(8);   // an empty cell, but the game is over
        Assert.Equal(end, Cells(over.Board));
        Assert.Equal(status, over.StatusText);
    }

    [Fact]
    public async Task NewGame_DuringASearch_ThrowsTheOldResultAway()
    {
        var held = new HeldSearch();
        var lab = new SearchLabViewModel(held.Search);
        var old = lab.PlayAsync(4);
        Assert.True(lab.IsThinking);
        held.WaitUntilStarted();
        var token = held.LastToken;

        await lab.NewGameAsync(Mark.X);
        Assert.True(token.IsCancellationRequested);
        Assert.False(lab.IsThinking);
        Assert.Equal(".........", Cells(lab.Board));
        Assert.Equal("Your move (you are X).", lab.StatusText);

        held.Release();   // the old search now comes back, late, with a result
        await old;
        Assert.Equal(".........", Cells(lab.Board));
        Assert.Empty(lab.LookAhead);
        Assert.Equal("", lab.CountText);
        Assert.False(lab.IsThinking);
        Assert.Equal("Your move (you are X).", lab.StatusText);
    }

    [Fact]
    public async Task NewGameAsO_DuringASearch_KeepsOnlyTheNewOpening()
    {
        var held = new HeldSearch();
        var lab = new SearchLabViewModel(held.Search);
        var old = lab.PlayAsync(4);   // held
        await lab.NewGameAsync(Mark.O);   // the second search is not held
        Assert.Equal("X........", Cells(lab.Board));
        Assert.Equal(9, lab.LookAhead.Count);
        held.Release();
        await old;
        Assert.Equal("X........", Cells(lab.Board));
        Assert.Equal(9, lab.LookAhead.Count);
        Assert.Equal("The computer looked at 549,946 positions to choose its move.", lab.CountText);
        Assert.Equal("Your move (you are O).", lab.StatusText);
    }

    [Fact]
    public async Task Deactivate_CancelsTheSearch_AndThrowsItsResultAway()
    {
        var held = new HeldSearch();
        var lab = new SearchLabViewModel(held.Search);
        var run = lab.PlayAsync(4);
        held.WaitUntilStarted();
        lab.Deactivate();
        Assert.True(held.LastToken.IsCancellationRequested);
        Assert.False(lab.IsThinking);
        held.Release();
        await run;
        Assert.Equal("....X....", Cells(lab.Board));
        Assert.Empty(lab.LookAhead);
    }

    [Fact]
    public async Task ACancelledSearch_ThatStopsEarly_IsQuietlyDropped()
    {
        // The real search: cancelled before it starts, Task.Run or the search throws OperationCanceledException, which the
        // lab swallows because its generation is over.
        var lab = new SearchLabViewModel((b, t) => { t.ThrowIfCancellationRequested(); return Minimax.Search(b, t); });
        var opening = lab.NewGameAsync(Mark.O);
        lab.Deactivate();
        await opening;   // does not throw
        Assert.False(lab.IsThinking);
    }

    [Fact]
    public async Task ASearchThatFails_StopsThinking_AndSaysSo()
    {
        var lab = new SearchLabViewModel((_, _) => throw new InvalidOperationException("test failure"));
        await lab.PlayAsync(4);   // does not throw: the failure is reported in the background and shown in words
        Assert.False(lab.IsThinking);
        Assert.Equal("....X....", Cells(lab.Board));
        Assert.Equal("The computer could not choose its move. Start a new game.", lab.StatusText);
        await lab.PlayAsync(0);   // not your turn: ignored
        Assert.Equal("....X....", Cells(lab.Board));
        await lab.NewGameAsync(Mark.X);
        Assert.Equal("Your move (you are X).", lab.StatusText);
    }

    [Fact]
    public async Task AFailedExpansion_StaysClosed_AndCanBeTriedAgain()
    {
        bool fail = false;
        var lab = new SearchLabViewModel((b, t) => fail ? throw new InvalidOperationException("test failure") : Minimax.Search(b, t));
        await lab.PlayAsync(4);
        var node = lab.LookAhead[0];
        fail = true;
        node.IsExpanded = true;
        Assert.False(node.IsExpanded);
        Assert.Empty(node.Children);
        fail = false;
        node.IsExpanded = true;   // tried again
        Assert.True(node.IsExpanded);
        Assert.Equal(7, node.Children.Count);
    }

    [Fact]
    public async Task WorkCompleted_CoversTheOpeningSearch()
    {
        var lab = new SearchLabViewModel();
        _ = lab.NewGameAsync(Mark.O);
        await lab.WorkCompleted;
        Assert.Equal("X........", Cells(lab.Board));
        Assert.Equal(9, lab.LookAhead.Count);
    }

    [Fact]
    public async Task Commands_StartGamesAndPlay()
    {
        var lab = new SearchLabViewModel();
        await lab.NewGameAsOCommand.ExecuteAsync(null);
        Assert.Equal("X........", Cells(lab.Board));
        await lab.PlayCommand.ExecuteAsync(4);
        Assert.Equal(2, Cells(lab.Board).Count(ch => ch == 'X'));   // the computer replied
        await lab.NewGameAsXCommand.ExecuteAsync(null);
        Assert.Equal(".........", Cells(lab.Board));
        Assert.Equal(Mark.X, lab.Human);
    }

    [Fact]
    public async Task NewGame_WithNoMark_Throws()
    {
        var lab = new SearchLabViewModel();
        await Assert.ThrowsAsync<ArgumentException>(() => lab.NewGameAsync(Mark.None));
    }

    [Fact]
    public async Task Reset_StartsANewGameAsX()
    {
        var lab = new SearchLabViewModel();
        await lab.NewGameAsync(Mark.O);
        lab.Reset();
        await lab.WorkCompleted;
        Assert.Equal(Mark.X, lab.Human);
        Assert.Equal(".........", Cells(lab.Board));
        Assert.Empty(lab.LookAhead);
        Assert.Equal("", lab.CountText);
    }

    [Fact]
    public async Task Screens_AreBaselines_AndLightOncePerMove()
    {
        var lab = new SearchLabViewModel();
        Assert.Equal(["board", "lookahead"], lab.ReportedScreens);
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        await lab.PlayAsync(4);   // the human's mark and the computer's reply: one move, one light each
        Assert.Equal(1, raised.Count(s => s == "board"));
        Assert.Equal(1, raised.Count(s => s == "lookahead"));
        raised.Clear();

        lab.LookAhead[0].IsExpanded = true;   // opening a branch only shows more; it lights nothing
        await lab.PlayAsync(4);   // ignored
        Assert.Empty(raised);

        await lab.NewGameAsync(Mark.O);   // cleared, then the computer's opening: one light each
        Assert.Equal(1, raised.Count(s => s == "board"));
        Assert.Equal(1, raised.Count(s => s == "lookahead"));
        raised.Clear();

        var fresh = new SearchLabViewModel();
        fresh.ScreenChanged += (_, e) => raised.Add(e.Screen);
        await fresh.NewGameAsync(Mark.X);   // nothing changes
        Assert.Empty(raised);
    }
}
