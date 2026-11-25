using AIAnatomy.Core;

namespace AIAnatomy.Models.Reinforcement;

/// <summary>
/// The rewards and the learning settings. <see cref="ExplorationRate"/> is the starting value;
/// the agent's own property may change afterwards.
/// </summary>
public sealed record QSettings(
    double GoalReward = 10,
    double StepCost = 1,
    double Discount = 0.9,
    double ExplorationRate = 0.2,
    double LearningRate = 0.5,
    int MaxStepsPerEpisode = 200);

/// <summary>What one step did. <c>EpisodeEnded</c> is true when the goal was reached or the step cap was hit.</summary>
public sealed record StepResult(GridCell From, Move Move, GridCell To, double Reward, bool EpisodeEnded);

/// <summary>A tabular Q-learning agent that learns to cross a <see cref="GridWorld"/> by trial and reward.</summary>
public sealed class QLearningAgent
{
    private static readonly Move[] Moves = Enum.GetValues<Move>();

    private readonly double[,,] _q;
    private readonly SeededRandom _random;

    public QLearningAgent(GridWorld world, QSettings settings, int seed)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(settings);
        RequireRate(settings.ExplorationRate, nameof(settings.ExplorationRate));
        RequireRate(settings.LearningRate, nameof(settings.LearningRate));
        RequireRate(settings.Discount, nameof(settings.Discount));
        if (!(settings.StepCost >= 0))
            throw new ArgumentException("The step cost must be zero or more.", nameof(settings));
        if (settings.MaxStepsPerEpisode < 1)
            throw new ArgumentException("An episode needs at least one step.", nameof(settings));

        World = world;
        Settings = settings;
        ExplorationRate = settings.ExplorationRate;
        _q = new double[world.Columns, world.Rows, Moves.Length];
        _random = new SeededRandom(seed);
        Position = world.Start;
    }

    public GridWorld World { get; }
    public QSettings Settings { get; }

    /// <summary>The chance of trying a random move; may be changed while learning.</summary>
    public double ExplorationRate { get; set; }

    public GridCell Position { get; private set; }
    public int Episodes { get; private set; }
    public int StepsThisEpisode { get; private set; }

    public double Q(GridCell cell, Move move) => _q[cell.Column, cell.Row, (int)move];

    /// <summary>The best Q over the four moves; 0 at the goal, which ends the episode.</summary>
    public double Value(GridCell cell)
    {
        if (cell == World.Goal) return 0;
        double best = Q(cell, Moves[0]);
        for (int i = 1; i < Moves.Length; i++) best = Math.Max(best, _q[cell.Column, cell.Row, i]);
        return best;
    }

    /// <summary>
    /// The one move with the strictly highest Q. Null at the goal, on a wall, or when two or more moves tie for best
    /// (all four are equal before anything is learned there). The agent breaks such ties at random when it moves, so an
    /// arrow for a tied square would point at a move it does not favour.
    /// </summary>
    public Move? Preferred(GridCell cell)
    {
        if (cell == World.Goal || World.IsWall(cell)) return null;
        Move best = Moves[0];
        double bestValue = Q(cell, Moves[0]);
        bool tied = false;
        for (int i = 1; i < Moves.Length; i++)
        {
            double q = _q[cell.Column, cell.Row, i];
            if (q > bestValue) { bestValue = q; best = Moves[i]; tied = false; }
            else if (q == bestValue) tied = true;
        }
        return tied ? null : best;
    }

    /// <summary>Makes one move and learns from it.</summary>
    public StepResult Step()
    {
        GridCell from = Position;
        Move move = ChooseMove(from);
        GridCell to = World.Next(from, move);
        bool reachedGoal = to == World.Goal;
        double reward = reachedGoal ? Settings.GoalReward : -Settings.StepCost;

        // V(goal) is 0, so nothing is added after the final step. A wall bump has to == from.
        double target = reward + Settings.Discount * Value(to);
        _q[from.Column, from.Row, (int)move] += Settings.LearningRate * (target - Q(from, move));

        StepsThisEpisode++;
        bool ended = reachedGoal || StepsThisEpisode >= Settings.MaxStepsPerEpisode;
        if (ended)
        {
            Episodes++;
            StepsThisEpisode = 0;
            Position = World.Start;
        }
        else
        {
            Position = to;
        }
        return new StepResult(from, move, to, reward, ended);
    }

    /// <summary>Steps until the goal or the step cap; returns the number of steps taken.</summary>
    public int RunEpisode()
    {
        int steps = 0;
        StepResult result;
        do
        {
            result = Step();
            steps++;
        }
        while (!result.EpisodeEnded);
        return steps;
    }

    private Move ChooseMove(GridCell cell)
    {
        if (_random.NextDouble() < ExplorationRate)
            return Moves[_random.NextInt(Moves.Length)];

        // Greedy: the highest Q, ties broken at random (Preferred shows no arrow for a tie).
        double best = double.NegativeInfinity;
        var tied = new List<Move>(Moves.Length);
        foreach (Move m in Moves)
        {
            double q = Q(cell, m);
            if (q > best) { best = q; tied.Clear(); tied.Add(m); }
            else if (q == best) tied.Add(m);
        }
        return tied[_random.NextInt(tied.Count)];
    }

    private static void RequireRate(double value, string name)
    {
        if (!(value >= 0 && value <= 1))
            throw new ArgumentException($"{name} must be between 0 and 1.", name);
    }
}
