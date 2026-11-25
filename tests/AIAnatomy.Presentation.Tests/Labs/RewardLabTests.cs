using AIAnatomy.Models.Reinforcement;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class RewardLabTests
{
    private static readonly GridCell[] DefaultWalls =
        [new(1, 1), new(2, 1), new(3, 1), new(3, 2), new(3, 3), new(1, 3), new(1, 4), new(4, 4)];

    /// <summary>A lab whose runs never wait between updates, so tests depend on the episodes, not on the clock.</summary>
    private static RewardLabViewModel NewLab() => new() { UpdateInterval = TimeSpan.Zero };

    private static MazeSquare At(RewardLabViewModel lab, int column, int row) => lab.Squares[row * RewardLabViewModel.Columns + column];

    private static async Task<RewardLabViewModel> Trained(int episodes)
    {
        var lab = NewLab();
        lab.EpisodesPerRun = episodes;
        await lab.RunCommand.ExecuteAsync(null);
        Assert.Equal(episodes, lab.Episodes);
        return lab;
    }

    /// <summary>Steps from each open square to the goal, by breadth-first search over the maze's moves.</summary>
    private static Dictionary<GridCell, int> DistancesToGoal(GridWorld world)
    {
        var distance = new Dictionary<GridCell, int> { [world.Goal] = 0 };
        var queue = new Queue<GridCell>([world.Goal]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            for (int c = 0; c < world.Columns; c++)
            for (int r = 0; r < world.Rows; r++)
            {
                var other = new GridCell(c, r);
                if (distance.ContainsKey(other) || world.IsWall(other)) continue;
                if (Enum.GetValues<Move>().Any(m => world.Next(other, m) == cell))
                {
                    distance[other] = distance[cell] + 1;
                    queue.Enqueue(other);
                }
            }
        }
        return distance;
    }

    [Fact]
    public void Opens_OnTheDefaultMaze_WithNothingLearned()
    {
        var lab = NewLab();
        Assert.Equal("RewardLab", lab.LabId);
        Assert.Equal(RewardLabViewModel.Id, lab.LabId);
        Assert.Equal((6, 6, 300), (RewardLabViewModel.Columns, RewardLabViewModel.Rows, RewardLabViewModel.MaxEpisodesPerRun));
        Assert.Equal(36, lab.Squares.Count);
        for (int i = 0; i < 36; i++) Assert.Equal(new GridCell(i % 6, i / 6), lab.Squares[i].Cell);   // row by row
        Assert.Equal(DefaultWalls.OrderBy(c => (c.Row, c.Column)), lab.Squares.Where(s => s.IsWall).Select(s => s.Cell));
        Assert.Equal(new GridCell(0, 0), Assert.Single(lab.Squares, s => s.IsStart).Cell);
        Assert.Equal(new GridCell(5, 5), Assert.Single(lab.Squares, s => s.IsGoal).Cell);
        Assert.Equal(new GridCell(0, 0), Assert.Single(lab.Squares, s => s.HasAgent).Cell);
        Assert.All(lab.Squares, s => Assert.Equal(0, s.Value));
        Assert.All(lab.Squares, s => Assert.Null(s.Arrow));
        Assert.Equal((10.0, 1.0, 0.2, 0.9), (lab.GoalReward, lab.StepCost, lab.ExplorationRate, lab.Discount));
        Assert.Equal(1, lab.Seed);
        Assert.Equal(MazeTool.Wall, lab.Tool);
        Assert.Null(lab.Message);
        Assert.Equal(0, lab.Episodes);
        Assert.Equal("Episodes: 0. Nothing learned yet.", lab.EpisodesText);
        Assert.Empty(lab.StepsSeries);
        Assert.False(lab.IsRunning);
        Assert.True(lab.World.GoalReachable);
    }

    [Fact]
    public void SquareNames_SayWhatEachSquareIs()
    {
        var lab = NewLab();
        Assert.Equal("Square 1, 1: start, agent here, value 0.00, no single best move yet", At(lab, 0, 0).AutomationName);
        Assert.Equal("Square 2, 1: value 0.00, no single best move yet", At(lab, 1, 0).AutomationName);
        Assert.Equal("Square 2, 2: wall", At(lab, 1, 1).AutomationName);
        Assert.Equal("Square 6, 6: goal", At(lab, 5, 5).AutomationName);
        Assert.Equal("", At(lab, 1, 1).ValueText);
        Assert.Equal("0.00", At(lab, 1, 0).ValueText);
    }

    [Fact]
    public void StepOnce_MovesTheAgentAtMostOneSquare_AndSaysWhatHappened()
    {
        var lab = NewLab();
        var at = new GridCell(0, 0);
        bool moved = false;
        for (int i = 0; i < 20; i++)
        {
            lab.StepOnce();
            var now = Assert.Single(lab.Squares, s => s.HasAgent).Cell;
            Assert.True(Math.Abs(now.Column - at.Column) + Math.Abs(now.Row - at.Row) <= 1, $"{at} -> {now}");
            moved |= now != at;
            at = now;
            Assert.NotEqual("", lab.LastStepText);
        }
        Assert.True(moved);
        Assert.Equal(0, lab.Episodes);
        Assert.Equal("Episodes: 0. This one has taken 20 steps so far.", lab.EpisodesText);
        Assert.Contains(lab.Squares, s => s.Arrow != null || s.Value != 0);   // something was learned from the step costs
    }

    [Fact]
    public async Task Run300_TheGoalsNeighbourIsWorthMoreThanTheStart_AndArrowsFollowAShortestPath()
    {
        var lab = await Trained(RewardLabViewModel.MaxEpisodesPerRun);
        Assert.Equal(300, lab.StepsSeries.Single().Points.Count);
        Assert.Matches(@"^Episodes: 300\. The last one took \d+ steps?\.$", lab.EpisodesText);

        var distance = DistancesToGoal(lab.World);
        Assert.Equal(10, distance[new GridCell(0, 0)]);
        var start = At(lab, 0, 0);
        bool AlongAShortestPath(MazeSquare s) =>
            s.Arrow is { } move && distance.TryGetValue(lab.World.Next(s.Cell, move), out int d) && d == distance[s.Cell] - 1;

        // A square next to the goal, on a shortest path from the start, is worth more than the start, and points at the goal.
        var next = lab.Squares.Where(s => !s.IsWall && distance.GetValueOrDefault(s.Cell, -1) == 1).ToList();
        Assert.Contains(next, s => s.Value > start.Value && AlongAShortestPath(s));
        Assert.True(AlongAShortestPath(start), $"the start prefers {start.Arrow}");
        Assert.Equal(10, next.Max(s => s.Value), 1e-6);   // one step from the goal: the goal reward itself
    }

    [Fact]
    public async Task Stop_EndsARunWithinOneEpisode()
    {
        var lab = NewLab();
        lab.EpisodesPerUpdate = 50;   // the worker would run 50 episodes before showing them
        lab.AfterEpisode = n => { if (n == 3) lab.RunCancelCommand.Execute(null); };
        await lab.RunCommand.ExecuteAsync(null);
        Assert.Equal(3, lab.Episodes);
        Assert.Equal(3, lab.StepsSeries.Single().Points.Count);
        Assert.False(lab.IsRunning);
    }

    [Fact]
    public async Task MazeAndRewardChanges_ClearWhatWasLearned_WithTheMessage()
    {
        var changes = new (string What, Action<RewardLabViewModel> Change)[]
        {
            ("wall", l => { l.Tool = MazeTool.Wall; l.Apply(new GridCell(2, 2)); }),
            ("wall removed", l => { l.Tool = MazeTool.Wall; l.Apply(new GridCell(1, 1)); }),
            ("start", l => { l.Tool = MazeTool.Start; l.Apply(new GridCell(0, 1)); }),
            ("goal", l => { l.Tool = MazeTool.Goal; l.Apply(new GridCell(5, 4)); }),
            ("goal reward", l => l.GoalReward = 15),
            ("step cost", l => l.StepCost = 0.5),
            ("discount", l => l.Discount = 0.8),
            ("seed", l => l.Seed = 2),
        };
        foreach (var (what, change) in changes)
        {
            var lab = await Trained(20);
            change(lab);
            Assert.True(lab.Episodes == 0, what);
            Assert.All(lab.Squares, s => Assert.Equal(0, s.Value));
            Assert.All(lab.Squares, s => Assert.Null(s.Arrow));
            Assert.Empty(lab.StepsSeries);
            Assert.Equal(RewardLabViewModel.ChangedMessage, lab.Message);
            Assert.Equal("The maze or the rewards changed, so the agent starts learning again.", lab.Message);
        }

        var edited = NewLab();
        edited.Apply(new GridCell(2, 2));
        Assert.True(At(edited, 2, 2).IsWall);
        edited.Apply(new GridCell(2, 2));
        Assert.False(At(edited, 2, 2).IsWall);   // the wall tool toggles
        edited.Tool = MazeTool.Start;
        edited.Apply(new GridCell(0, 1));
        Assert.True(At(edited, 0, 1).IsStart && At(edited, 0, 1).HasAgent);
        Assert.False(At(edited, 0, 0).IsStart);
    }

    [Fact]
    public async Task ExplorationRate_DoesNotClearWhatWasLearned()
    {
        var lab = await Trained(20);
        var before = lab.Squares.ToArray();
        lab.ExplorationRate = 0.5;
        Assert.Equal(20, lab.Episodes);
        Assert.Equal(before, lab.Squares.ToArray());
        Assert.Null(lab.Message);
        Assert.Equal(0.5, lab.Settings.ExplorationRate);
    }

    [Fact]
    public async Task AWallOnTheStartOrGoal_OrTheStartOntoTheGoal_IsRefused()
    {
        var lab = await Trained(20);
        var before = lab.Squares.ToArray();
        void Refused(MazeTool tool, GridCell cell, string message)
        {
            lab.Tool = tool;
            lab.Apply(cell);
            Assert.Equal(message, lab.Message);
            Assert.Equal(before, lab.Squares.ToArray());
            Assert.Equal(20, lab.Episodes);
        }
        Refused(MazeTool.Wall, new GridCell(0, 0), RewardLabViewModel.WallOnStartMessage);
        Refused(MazeTool.Wall, new GridCell(5, 5), RewardLabViewModel.WallOnGoalMessage);
        Refused(MazeTool.Start, new GridCell(5, 5), RewardLabViewModel.StartOnGoalMessage);
        Refused(MazeTool.Goal, new GridCell(0, 0), RewardLabViewModel.StartOnGoalMessage);
        Refused(MazeTool.Start, new GridCell(1, 1), RewardLabViewModel.OnAWallMessage);
        Refused(MazeTool.Goal, new GridCell(1, 1), RewardLabViewModel.OnAWallMessage);
        Assert.Equal("A wall can't go on the start.", RewardLabViewModel.WallOnStartMessage);
        Assert.Equal("A wall can't go on the goal.", RewardLabViewModel.WallOnGoalMessage);

        lab.Tool = MazeTool.Start;
        lab.Apply(new GridCell(0, 0));   // the start onto itself changes nothing
        Assert.Equal(20, lab.Episodes);
    }

    [Fact]
    public async Task WallingOffTheGoal_SaysItCantBeReached_AndRunStillEndsAtTheCap()
    {
        var lab = NewLab();
        lab.Apply(new GridCell(4, 5));
        lab.Apply(new GridCell(5, 4));
        Assert.False(lab.World.GoalReachable);
        Assert.Equal(RewardLabViewModel.UnreachableMessage, lab.Message);
        Assert.Equal("The goal can't be reached from the start.", lab.Message);

        await lab.RunCommand.ExecuteAsync(null);
        Assert.Equal(RewardLabViewModel.MaxEpisodesPerRun, lab.Episodes);
        Assert.All(lab.StepsSeries.Single().Points, p => Assert.Equal(200, p.Y));
        Assert.Equal("Episodes: 300. The last one took 200 steps and did not reach the goal.", lab.EpisodesText);
        Assert.Equal(RewardLabViewModel.UnreachableMessage, lab.Message);   // still true after the run

        lab.Apply(new GridCell(5, 4));   // open again
        Assert.Equal(RewardLabViewModel.ChangedMessage, lab.Message);
    }

    [Fact]
    public async Task Reset_RestoresEverything()
    {
        var fresh = NewLab();
        var lab = await Trained(30);
        lab.Apply(new GridCell(2, 2));
        lab.Tool = MazeTool.Goal;
        lab.Apply(new GridCell(4, 0));
        lab.GoalReward = 5;
        lab.StepCost = 2;
        lab.ExplorationRate = 0.9;
        lab.Discount = 0.5;
        lab.Seed = 9;
        await lab.RunCommand.ExecuteAsync(null);

        lab.Reset();

        Assert.Equal(fresh.Squares, lab.Squares);
        Assert.Equal((10.0, 1.0, 0.2, 0.9), (lab.GoalReward, lab.StepCost, lab.ExplorationRate, lab.Discount));
        Assert.Equal((1, MazeTool.Wall, 0), (lab.Seed, lab.Tool, lab.Episodes));
        Assert.Null(lab.Message);
        Assert.Equal(fresh.EpisodesText, lab.EpisodesText);
        Assert.Empty(lab.StepsSeries);
        Assert.Equal("", lab.LastStepText);

        // And the learning after Reset is the same as a fresh lab's (same maze, settings and seed).
        lab.EpisodesPerRun = RewardLabViewModel.MaxEpisodesPerRun;   // a test hook, which Reset leaves alone
        await lab.RunCommand.ExecuteAsync(null);
        await fresh.RunCommand.ExecuteAsync(null);
        Assert.Equal(fresh.Squares, lab.Squares);
    }

    [Fact]
    public async Task Deactivate_CancelsARun_AndItsResultIsThrownAway()
    {
        var lab = new RewardLabViewModel();   // the real pace: 50 ms between updates
        var run = lab.RunCommand.ExecuteAsync(null);
        Assert.True(lab.IsRunning);
        lab.Deactivate();
        Assert.False(lab.IsRunning);
        await run;
        Assert.Equal(0, lab.Episodes);
        Assert.Equal("Episodes: 0. Nothing learned yet.", lab.EpisodesText);
    }

    [Fact]
    public async Task AClearingEditDuringARun_StopsTheRun()
    {
        var lab = new RewardLabViewModel();
        var run = lab.RunCommand.ExecuteAsync(null);
        lab.Apply(new GridCell(2, 2));
        Assert.False(lab.IsRunning);
        await run;
        Assert.Equal(0, lab.Episodes);
        Assert.True(At(lab, 2, 2).IsWall);
        Assert.Equal(RewardLabViewModel.ChangedMessage, lab.Message);
    }

    [Fact]
    public void Sliders_AreClampedToTheirRanges()
    {
        var lab = NewLab();
        lab.GoalReward = 50; Assert.Equal(20, lab.GoalReward);
        lab.GoalReward = 0; Assert.Equal(1, lab.GoalReward);
        lab.StepCost = -1; Assert.Equal(0, lab.StepCost);
        lab.StepCost = 3; Assert.Equal(2, lab.StepCost);
        lab.ExplorationRate = 1.5; Assert.Equal(1, lab.ExplorationRate);
        lab.ExplorationRate = -0.5; Assert.Equal(0, lab.ExplorationRate);
        lab.Discount = 1; Assert.Equal(0.99, lab.Discount);
        lab.Discount = 0.1; Assert.Equal(0.5, lab.Discount);
        lab.ExplorationRate = double.NaN; Assert.Equal(0.2, lab.ExplorationRate);
        Assert.Equal((1.0, 2.0, 0.5), (lab.Settings.GoalReward, lab.Settings.StepCost, lab.Settings.Discount));
    }

    [Fact]
    public async Task Screens_AreBaselines_AndARunLightsEachOnceAtItsEnd()
    {
        var lab = NewLab();
        Assert.Equal(["values", "episodes"], lab.ReportedScreens);
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.EpisodesPerRun = 12;
        lab.EpisodesPerUpdate = 2;   // six updates inside the one run
        await lab.RunCommand.ExecuteAsync(null);
        Assert.Equal(12, lab.Episodes);
        Assert.Equal(["episodes", "values"], raised.Order(StringComparer.Ordinal));
        raised.Clear();

        lab.ExplorationRate = 0.3;   // nothing shown changes
        Assert.Empty(raised);
        lab.StepOnce();   // a direct action
        Assert.Contains("values", raised);
    }

    // ---- the lesson's Try this, guarded ----

    /// <summary>The hand-worked value of a square d steps from the goal: 10, then -1 + 0.9 times the value one step nearer.</summary>
    private static double HandValue(int d) => d == 1 ? 10 : -1 + 0.9 * HandValue(d - 1);

    [Fact]
    public async Task TryThis_ARunsEpisodesFallFromOverAHundredStepsToUnderTwenty()
    {
        var lab = await Trained(RewardLabViewModel.MaxEpisodesPerRun);
        var steps = lab.StepsSeries.Single().Points.Select(p => p.Y).ToList();
        Assert.True(steps[0] > 100, $"first episode {steps[0]}");
        Assert.All(steps.TakeLast(10), y => Assert.InRange(y, 10, 20));
        Assert.Equal(-2.25, At(lab, 0, 0).Value, 0.005);   // the start, 10 steps out, has its hand-worked value
        Assert.Equal(HandValue(10), At(lab, 0, 0).Value, 0.005);
    }

    [Fact]
    public async Task TryThis_ExploringAlways_GivesEverySquareItsHandWorkedValue_ButLongEpisodes()
    {
        var lab = NewLab();
        lab.ExplorationRate = 1;
        await lab.RunCommand.ExecuteAsync(null);
        var distance = DistancesToGoal(lab.World);
        foreach (var s in lab.Squares.Where(s => !s.IsWall && !s.IsGoal))
            Assert.True(Math.Abs(HandValue(distance[s.Cell]) - s.Value) < 0.005, $"{s.AutomationName}, hand value {HandValue(distance[s.Cell])}");
        Assert.True(lab.StepsSeries.Single().Points.TakeLast(10).Average(p => p.Y) > 50);
    }

    [Fact]
    public async Task TryThis_NeverExploring_FindsATenStepPath_ButLeavesOtherSquaresWrong()
    {
        var lab = NewLab();
        lab.ExplorationRate = 0;
        await lab.RunCommand.ExecuteAsync(null);
        Assert.All(lab.StepsSeries.Single().Points.TakeLast(10), p => Assert.Equal(10, p.Y));
        var distance = DistancesToGoal(lab.World);
        Assert.Contains(lab.Squares, s => !s.IsWall && !s.IsGoal && Math.Abs(HandValue(distance[s.Cell]) - s.Value) > 1);
    }

    // ---- review fixes ----

    [Fact]
    public void ValueText_AlwaysHasTwoDecimals_WithATrueMinus()
    {
        Assert.Equal("8.00", RewardLabViewModel.ValueString(8));
        Assert.Equal("10.00", RewardLabViewModel.ValueString(10));
        Assert.Equal("−2.25", RewardLabViewModel.ValueString(-2.2515));
        Assert.Equal("0.00", RewardLabViewModel.ValueString(-0.001));   // never "−0.00"
        Assert.Equal("6.20", RewardLabViewModel.ValueString(6.2));
    }

    [Fact]
    public async Task ValueTexts_AfterARun_AllHaveTwoDecimals()
    {
        var lab = await Trained(RewardLabViewModel.MaxEpisodesPerRun);
        foreach (var s in lab.Squares.Where(s => !s.IsWall && !s.IsGoal))
        {
            Assert.Matches(@"^−?\d+\.\d\d$", s.ValueText);
            Assert.Contains($"value {s.ValueText}", s.AutomationName);
        }
    }

    /// <summary>
    /// Starts a run that pauses on the worker after episode <paramref name="pauseAfter"/> (the agent's count), and returns
    /// the run and a way to let it carry on. Waiting on the pause is waiting for an event, not for time.
    /// </summary>
    private static (Task Run, Action Release) PausedRun(RewardLabViewModel lab, int pauseAfter)
    {
        var reached = new ManualResetEventSlim(false);
        var gate = new ManualResetEventSlim(false);
        lab.AfterEpisode = n =>
        {
            if (n != pauseAfter) return;
            reached.Set();
            gate.Wait(TimeSpan.FromSeconds(30));
        };
        var run = lab.RunCommand.ExecuteAsync(null);
        Assert.True(reached.Wait(TimeSpan.FromSeconds(30)), "the run never reached the pause");
        return (run, gate.Set);
    }

    [Fact]
    public async Task ExplorationRateChangedInTheLastBatch_ReachesTheAgentWhenTheRunEnds()
    {
        var lab = NewLab();
        lab.EpisodesPerUpdate = RewardLabViewModel.MaxEpisodesPerRun;   // one batch: the rate is never handed over between batches
        var (run, release) = PausedRun(lab, 1);
        lab.ExplorationRate = 0;   // while the only batch is playing
        release();
        await run;
        Assert.Equal(300, lab.Episodes);

        // Never exploring, the trained agent walks straight along its arrows to the goal: 10 steps, one episode.
        for (int i = 0; i < 10; i++)
        {
            var from = Assert.Single(lab.Squares, s => s.HasAgent);
            lab.StepOnce();
            Assert.StartsWith($"Moved {RewardLabViewModel.MoveName(from.Arrow!.Value)} ", lab.LastStepText);
        }
        Assert.Equal(301, lab.Episodes);
        Assert.Contains("into the goal", lab.LastStepText);
    }

    [Fact]
    public async Task ResetDuringARun_LightsEachScreenOnce_ForItsOwnResult()
    {
        var lab = await Trained(20);
        lab.EpisodesPerUpdate = 1;   // several updates before the pause, so the screens have moved on during the run
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        var (run, release) = PausedRun(lab, 24);
        lab.Reset();
        release();
        await run;
        Assert.Equal(["episodes", "values"], raised.Order(StringComparer.Ordinal));
        Assert.Equal(NewLab().Squares, lab.Squares);
        Assert.Equal(0, lab.Episodes);
    }

    [Fact]
    public async Task SeedChangeDuringARun_LightsEachScreenOnce_AndSaysLearningStartsAgain()
    {
        var lab = await Trained(20);
        lab.EpisodesPerUpdate = 1;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        var (run, release) = PausedRun(lab, 24);
        lab.Seed = 2;
        Assert.False(lab.IsRunning);
        release();
        await run;
        Assert.Equal(["episodes", "values"], raised.Order(StringComparer.Ordinal));
        Assert.Equal(RewardLabViewModel.ChangedMessage, lab.Message);
        Assert.Equal(0, lab.Episodes);
    }

    [Fact]
    public void ARefusedEdit_LightsTheValuesScreenOnly()
    {
        var lab = NewLab();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);
        lab.Apply(new GridCell(0, 0));   // a wall on the start
        Assert.Equal(["values"], raised);
        raised.Clear();
        lab.Apply(new GridCell(0, 0));   // the same refusal again: nothing new to see
        Assert.Empty(raised);
    }

    [Fact]
    public void TryThis_AfterOneStep_NoArrow_ThenArrowsAppearWithinAFewSteps()
    {
        var lab = NewLab();
        lab.StepOnce();
        // The move just made now scores lower; the three untried moves still tie at 0, so no single move is best.
        Assert.All(lab.Squares, s => Assert.Null(s.Arrow));
        Assert.Contains("no single best move yet", At(lab, 0, 0).AutomationName);

        for (int i = 0; i < 9 && lab.Squares.All(s => s.Arrow == null); i++) lab.StepOnce();
        Assert.Contains(lab.Squares, s => s.Arrow != null);   // "after a few steps arrows appear"
    }
}
