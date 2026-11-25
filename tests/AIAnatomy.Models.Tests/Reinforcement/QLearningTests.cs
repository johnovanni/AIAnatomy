using AIAnatomy.Models.Reinforcement;

namespace AIAnatomy.Models.Tests.Reinforcement;

public class QLearningTests
{
    private static GridWorld Open() => new(3, 2, [], new GridCell(0, 0), new GridCell(2, 0));

    private static GridWorld WallInMiddle() =>
        new(3, 2, [new GridCell(1, 0)], new GridCell(0, 0), new GridCell(2, 0));

    private static GridWorld WalledOff() =>
        new(3, 2, [new GridCell(1, 0), new GridCell(1, 1)], new GridCell(0, 0), new GridCell(2, 0));

    private static QLearningAgent Explorer(GridWorld w, int seed = 1) =>
        new(w, new QSettings(ExplorationRate: 1, LearningRate: 0.5), seed);

    [Fact]
    public void OpenTwoByThree_ConvergesToTheHandValues()
    {
        var agent = Explorer(Open());
        for (int i = 0; i < 5000; i++) agent.RunEpisode();

        Assert.Equal(10.0, agent.Value(new GridCell(1, 0)), 1e-3);
        Assert.Equal(8.0, agent.Value(new GridCell(0, 0)), 1e-3);
        Assert.Equal(10.0, agent.Value(new GridCell(2, 1)), 1e-3);
        Assert.Equal(8.0, agent.Value(new GridCell(1, 1)), 1e-3);
        Assert.Equal(6.2, agent.Value(new GridCell(0, 1)), 1e-3);
        Assert.Equal(0.0, agent.Value(new GridCell(2, 0)));
        Assert.Equal(Move.Right, agent.Preferred(new GridCell(1, 0)));
        Assert.Null(agent.Preferred(new GridCell(2, 0)));
    }

    [Fact]
    public void WallInTheMiddle_ConvergesAroundIt()
    {
        var agent = Explorer(WallInMiddle());
        for (int i = 0; i < 5000; i++) agent.RunEpisode();

        Assert.Equal(10.0, agent.Value(new GridCell(2, 1)), 1e-3);
        Assert.Equal(8.0, agent.Value(new GridCell(1, 1)), 1e-3);
        Assert.Equal(6.2, agent.Value(new GridCell(0, 1)), 1e-3);
        Assert.Equal(4.58, agent.Value(new GridCell(0, 0)), 1e-3);
        Assert.Equal(Move.Down, agent.Preferred(new GridCell(0, 0)));
        Assert.Null(agent.Preferred(new GridCell(1, 0))); // a wall
    }

    [Fact]
    public void Next_StaysPutAtEdgesAndWalls()
    {
        var w = WallInMiddle();
        Assert.Equal(new GridCell(0, 0), w.Next(new GridCell(0, 0), Move.Up));
        Assert.Equal(new GridCell(0, 0), w.Next(new GridCell(0, 0), Move.Left));
        Assert.Equal(new GridCell(0, 0), w.Next(new GridCell(0, 0), Move.Right)); // wall
        Assert.Equal(new GridCell(0, 1), w.Next(new GridCell(0, 0), Move.Down));
        Assert.Equal(new GridCell(2, 1), w.Next(new GridCell(2, 1), Move.Right));
        Assert.Equal(new GridCell(2, 1), w.Next(new GridCell(2, 1), Move.Down));
        Assert.Equal(new GridCell(2, 0), w.Next(new GridCell(2, 1), Move.Up));
        Assert.True(w.IsWall(new GridCell(1, 0)));
        Assert.False(w.IsWall(new GridCell(0, 0)));
    }

    [Fact]
    public void BumpingAWall_CostsAStep()
    {
        var agent = Explorer(WallInMiddle(), seed: 3);
        for (int i = 0; i < 500; i++)
        {
            GridCell pos = agent.Position;
            double valueBefore = agent.Value(pos);
            double[] qBefore = Enum.GetValues<Move>().Select(m => agent.Q(pos, m)).ToArray();
            StepResult step = agent.Step();
            if (step.From == step.To)
            {
                Assert.Equal(-1.0, step.Reward);
                double expected = qBefore[(int)step.Move]
                    + 0.5 * (-1 + 0.9 * valueBefore - qBefore[(int)step.Move]);
                Assert.Equal(expected, agent.Q(pos, step.Move), 1e-12);
                return;
            }
        }
        Assert.Fail("No wall bump happened in 500 steps.");
    }

    [Fact]
    public void Preferred_IsNullBeforeLearning()
    {
        var agent = Explorer(Open());
        foreach (var cell in new[] { new GridCell(0, 0), new GridCell(1, 1), new GridCell(2, 0) })
            Assert.Null(agent.Preferred(cell));
    }

    [Fact]
    public void SameSeed_SameTable()
    {
        var a = Explorer(WallInMiddle(), seed: 7);
        var b = Explorer(WallInMiddle(), seed: 7);
        for (int i = 0; i < 100; i++) { a.RunEpisode(); b.RunEpisode(); }
        for (int c = 0; c < 3; c++)
            for (int r = 0; r < 2; r++)
                foreach (var m in Enum.GetValues<Move>())
                    Assert.Equal(a.Q(new GridCell(c, r), m), b.Q(new GridCell(c, r), m));
        Assert.Equal(a.Episodes, b.Episodes);
    }

    [Fact]
    public void GoalWalledOff_IsNotReachable_AndEpisodesStopAtTheCap()
    {
        var w = WalledOff();
        Assert.False(w.GoalReachable);
        Assert.True(Open().GoalReachable);
        Assert.True(WallInMiddle().GoalReachable);

        var agent = new QLearningAgent(w, new QSettings(ExplorationRate: 1, MaxStepsPerEpisode: 25), 1);
        Assert.Equal(25, agent.RunEpisode());
        Assert.Equal(1, agent.Episodes);
        Assert.Equal(new GridCell(0, 0), agent.Position);
        Assert.Equal(0, agent.StepsThisEpisode);
    }

    [Fact]
    public void EpisodeEnded_IsTrueOnTheCappedStep()
    {
        var agent = new QLearningAgent(WalledOff(), new QSettings(MaxStepsPerEpisode: 3), 1);
        Assert.False(agent.Step().EpisodeEnded);
        Assert.False(agent.Step().EpisodeEnded);
        Assert.True(agent.Step().EpisodeEnded);
    }

    [Fact]
    public void ReachingTheGoal_EndsTheEpisodeWithTheGoalReward()
    {
        var w = new GridWorld(2, 2, [], new GridCell(0, 0), new GridCell(1, 0));
        var agent = Explorer(w);
        StepResult r;
        do { r = agent.Step(); } while (!r.EpisodeEnded);
        Assert.Equal(new GridCell(1, 0), r.To);
        Assert.Equal(10.0, r.Reward);
        Assert.Equal(1, agent.Episodes);
        Assert.Equal(w.Start, agent.Position);
    }

    [Fact]
    public void Constructor_RejectsBadWorlds()
    {
        var s = new GridCell(0, 0);
        var g = new GridCell(1, 1);
        Assert.Throws<ArgumentException>(() => new GridWorld(2, 2, [s], s, g));
        Assert.Throws<ArgumentException>(() => new GridWorld(2, 2, [g], s, g));
        Assert.Throws<ArgumentException>(() => new GridWorld(2, 2, [], s, s));
        Assert.Throws<ArgumentException>(() => new GridWorld(1, 2, [], s, g));
        Assert.Throws<ArgumentException>(() => new GridWorld(13, 2, [], s, g));
        Assert.Throws<ArgumentException>(() => new GridWorld(2, 2, [new GridCell(5, 5)], s, g));
        Assert.Throws<ArgumentException>(() => new GridWorld(2, 2, [], s, new GridCell(2, 0)));
    }

    [Fact]
    public void Constructor_RejectsBadSettings()
    {
        var w = Open();
        Assert.Throws<ArgumentException>(() => new QLearningAgent(w, new QSettings(ExplorationRate: 1.5), 1));
        Assert.Throws<ArgumentException>(() => new QLearningAgent(w, new QSettings(LearningRate: -0.1), 1));
        Assert.Throws<ArgumentException>(() => new QLearningAgent(w, new QSettings(Discount: 2), 1));
        Assert.Throws<ArgumentException>(() => new QLearningAgent(w, new QSettings(StepCost: -1), 1));
        Assert.Throws<ArgumentException>(() => new QLearningAgent(w, new QSettings(MaxStepsPerEpisode: 0), 1));
    }

    [Fact]
    public void ExplorationRate_CanChangeWhileLearning()
    {
        var agent = new QLearningAgent(Open(), new QSettings(ExplorationRate: 0.3), 1);
        Assert.Equal(0.3, agent.ExplorationRate);
        agent.ExplorationRate = 0;
        Assert.Equal(0, agent.ExplorationRate);
        Assert.Equal(0.3, agent.Settings.ExplorationRate);
    }
}
