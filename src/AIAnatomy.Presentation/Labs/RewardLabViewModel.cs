using System.Diagnostics;
using System.Globalization;
using AIAnatomy.Models.Reinforcement;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GridCell = AIAnatomy.Models.Reinforcement.GridCell;

namespace AIAnatomy.Presentation.Labs;

/// <summary>What a click (or Space) on a square of the maze does.</summary>
public enum MazeTool { Wall, Start, Goal }

/// <summary>
/// One square of the maze as the view shows it. <see cref="Value"/> is the agent's value for the square (the best of its
/// four move values; 0 on a wall and at the goal), <see cref="ValueText"/> that value to two decimals ("" on a wall and at
/// the goal), and <see cref="Arrow"/> the move it prefers there (null when no single move is best).
/// </summary>
public sealed record MazeSquare(GridCell Cell, bool IsWall, bool IsStart, bool IsGoal, bool HasAgent,
    double Value, string ValueText, Move? Arrow, string AutomationName);

/// <summary>
/// Lesson 4.5's lab: a tabular Q-learning agent learning to cross an editable 6 × 6 maze from rewards alone. Squares are
/// shaded by the agent's value and show the move it prefers. Step makes one move; Run plays whole episodes on a worker
/// thread until Stop or <see cref="EpisodesPerRun"/>, showing them at most every <see cref="UpdateInterval"/>.
/// Screens: "values" (the maze) and "episodes" (the episodes line and the steps chart).
/// </summary>
/// <remarks>
/// The agent is only ever touched by one thread at a time: the worker during a batch of episodes, otherwise the UI thread.
/// An edit that clears learning during a run first ends the run (its generation), so the worker's agent is simply dropped;
/// the exploration rate, which does not clear learning, reaches the running agent between batches.
/// </remarks>
public sealed partial class RewardLabViewModel : LabViewModel
{
    public const string Id = "RewardLab";
    public const int Columns = 6, Rows = 6, MaxEpisodesPerRun = 300;

    /// <summary>A run shows its episodes this many at a time (fewer if the interval has not passed, never more).</summary>
    public const int DefaultEpisodesPerUpdate = 5;

    public const double DefaultGoalReward = 10, MinGoalReward = 1, MaxGoalReward = 20;
    public const double DefaultStepCost = 1, MinStepCost = 0, MaxStepCost = 2;
    public const double DefaultExplorationRate = 0.2, MinExplorationRate = 0, MaxExplorationRate = 1;
    public const double DefaultDiscount = 0.9, MinDiscount = 0.5, MaxDiscount = 0.99;

    public const string ChangedMessage = "The maze or the rewards changed, so the agent starts learning again.";
    public const string UnreachableMessage = "The goal can't be reached from the start.";
    public const string WallOnStartMessage = "A wall can't go on the start.";
    public const string WallOnGoalMessage = "A wall can't go on the goal.";
    public const string StartOnGoalMessage = "The start and the goal must be different squares.";
    public const string OnAWallMessage = "The start and the goal can't go on a wall. Remove the wall first.";

    /// <summary>The defaults Reset restores: walls at (1,1), (2,1), (3,1), (3,2), (3,3), (1,3), (1,4) and (4,4), counted
    /// (column, row) from 0 at the top left.</summary>
    public static IReadOnlyList<GridCell> DefaultWalls { get; } =
        [new(1, 1), new(2, 1), new(3, 1), new(3, 2), new(3, 3), new(1, 3), new(1, 4), new(4, 4)];

    public static GridCell DefaultStart => new(0, 0);
    public static GridCell DefaultGoal => new(5, 5);

    /// <summary>The least time between two updates during a run: at most 20 a second.</summary>
    public static readonly TimeSpan DefaultUpdateInterval = TimeSpan.FromMilliseconds(50);

    private const string ValuesScreen = "values", EpisodesScreen = "episodes";

    private readonly HashSet<GridCell> _walls = [];
    private GridCell _start = DefaultStart, _goal = DefaultGoal;
    private QLearningAgent _agent = null!;
    private List<(double X, double Y)> _steps = [];
    private int _lastSteps;
    private bool _lastReached, _reachable, _changed, _quiet;
    private string? _refusal;
    private int _generation;
    private IDisposable? _run;
    private int _episodesPerRun = MaxEpisodesPerRun, _episodesPerUpdate = DefaultEpisodesPerUpdate;

    public RewardLabViewModel()
    {
        _walls.UnionWith(DefaultWalls);
        NewAgent();
        Refresh();   // also reports the baselines
    }

    public override string LabId => Id;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsWallTool), nameof(IsStartTool), nameof(IsGoalTool))]
    private MazeTool tool = MazeTool.Wall;

    /// <summary>The reward for stepping into the goal: 1 to 20.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(GoalRewardText))] private double goalReward = DefaultGoalReward;

    /// <summary>What every other move costs, a bump into a wall or the edge included: 0 to 2.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(StepCostText))] private double stepCost = DefaultStepCost;

    /// <summary>The chance of trying a random move instead of the best known one: 0 to 1.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ExplorationRateText))] private double explorationRate = DefaultExplorationRate;

    /// <summary>How much a reward one step later counts: 0.5 to 0.99.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(DiscountText))] private double discount = DefaultDiscount;

    /// <summary>The 36 squares, row by row from the top left.</summary>
    [ObservableProperty] private IReadOnlyList<MazeSquare> squares = [];

    /// <summary>"Episodes: 42. The last one took 17 steps."</summary>
    [ObservableProperty] private string episodesText = "";

    /// <summary>Steps per episode (x: the episode, from 1); empty before the first episode ends.</summary>
    [ObservableProperty] private IReadOnlyList<LineSeries> stepsSeries = [];

    /// <summary>A refusal, the unreachable goal, or that learning starts again; null otherwise.</summary>
    [ObservableProperty] private string? message;

    /// <summary>What the last press of Step did, in words; empty after a run, an edit or Reset.</summary>
    [ObservableProperty] private string lastStepText = "";

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(StepCommand))] private bool isRunning;

    /// <summary>Episodes finished since the agent last started learning, as shown.</summary>
    [ObservableProperty] private int episodes;

    public bool IsWallTool { get => Tool == MazeTool.Wall; set { if (value) Tool = MazeTool.Wall; } }
    public bool IsStartTool { get => Tool == MazeTool.Start; set { if (value) Tool = MazeTool.Start; } }
    public bool IsGoalTool { get => Tool == MazeTool.Goal; set { if (value) Tool = MazeTool.Goal; } }

    public string GoalRewardText => $"Goal reward: {NumberText.Tick(GoalReward)}";
    public string StepCostText => $"Step cost: {NumberText.Tick(StepCost)}";
    public string ExplorationRateText => $"Exploration: {NumberText.Tick(ExplorationRate)}";
    public string DiscountText => $"Discount: {NumberText.Tick(Discount)}";

    /// <summary>The maze the agent is learning in now.</summary>
    public GridWorld World => _agent.World;

    /// <summary>The rewards and settings the lab gives a new agent (its learning rate and step limit are the model's defaults).</summary>
    public QSettings Settings => new(GoalReward, StepCost, Discount, ExplorationRate);

    /// <summary>How many episodes one press of Run plays: 1 to <see cref="MaxEpisodesPerRun"/> (the self-test lowers it).</summary>
    public int EpisodesPerRun
    {
        get => _episodesPerRun;
        set => _episodesPerRun = Math.Clamp(value, 1, MaxEpisodesPerRun);
    }

    /// <summary>A run shows its episodes at most this many at a time (at least 1; a test may raise it).</summary>
    public int EpisodesPerUpdate
    {
        get => _episodesPerUpdate;
        set => _episodesPerUpdate = Math.Max(1, value);
    }

    /// <summary>The least time between two updates during a run (a test or a render sets it to zero).</summary>
    public TimeSpan UpdateInterval { get; set; } = DefaultUpdateInterval;

    /// <summary>A test hook, called on the worker after each episode of a run with the agent's episode count.</summary>
    public Action<int>? AfterEpisode { get; set; }

    /// <summary>"Square 3, 2": column then row, counted from 1 at the top left.</summary>
    public static string SquareName(GridCell cell) => $"Square {cell.Column + 1}, {cell.Row + 1}";

    /// <summary>A square's value as the lab shows it: always two decimals, with the true minus sign ("8.00", "10.00",
    /// "−2.25"); a value that rounds to zero is "0.00", never "−0.00".</summary>
    public static string ValueString(double value)
    {
        double rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded == 0) rounded = 0;   // drops the sign of −0
        return rounded.ToString("0.00", CultureInfo.InvariantCulture).Replace('-', '−');
    }

    /// <summary>"up", "right", "down" or "left".</summary>
    public static string MoveName(Move move) => move switch
    {
        Move.Up => "up",
        Move.Right => "right",
        Move.Down => "down",
        _ => "left",
    };

    /// <summary>
    /// Uses the current <see cref="Tool"/> on a square. The wall tool adds a wall or takes one away; the start and goal tools
    /// move the start or the goal there. A wall on the start or the goal, the start onto the goal (or the goal onto the
    /// start), or either onto a wall is refused: <see cref="Message"/> says why and nothing changes. Any other change clears
    /// what the agent has learned (and ends a run).
    /// </summary>
    public void Apply(GridCell cell)
    {
        if (cell.Column is < 0 or >= Columns || cell.Row is < 0 or >= Rows)
            throw new ArgumentOutOfRangeException(nameof(cell), $"{cell} is not in the maze.");
        switch (Tool)
        {
            case MazeTool.Wall:
                if (cell == _start) { Refuse(WallOnStartMessage); return; }
                if (cell == _goal) { Refuse(WallOnGoalMessage); return; }
                if (!_walls.Remove(cell)) _walls.Add(cell);
                break;
            case MazeTool.Start:
                if (cell == _start) return;
                if (cell == _goal) { Refuse(StartOnGoalMessage); return; }
                if (_walls.Contains(cell)) { Refuse(OnAWallMessage); return; }
                _start = cell;
                break;
            default:
                if (cell == _goal) return;
                if (cell == _start) { Refuse(StartOnGoalMessage); return; }
                if (_walls.Contains(cell)) { Refuse(OnAWallMessage); return; }
                _goal = cell;
                break;
        }
        Restart();
    }

    /// <summary>Makes one move and learns from it (ignored during a run).</summary>
    public void StepOnce()
    {
        if (IsRunning) return;
        _refusal = null;
        _changed = false;
        int before = _agent.StepsThisEpisode;
        var result = _agent.Step();
        if (result.EpisodeEnded) Record(before + 1, result.To == _agent.World.Goal);
        LastStepText = StepText(result);
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanStep))]
    private void Step() => StepOnce();

    private bool CanStep() => !IsRunning;

    /// <summary>Plays episodes on a worker until Stop or <see cref="EpisodesPerRun"/>; the screens light once, at the end.</summary>
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task RunAsync(CancellationToken token)
    {
        int generation = _generation;
        var agent = _agent;
        _refusal = null;
        _changed = false;
        LastStepText = "";
        IsRunning = true;
        _run = BeginRun();
        try
        {
            Refresh();
            int target = EpisodesPerRun, done = 0;
            var sinceUpdate = Stopwatch.StartNew();
            while (done < target && !token.IsCancellationRequested)
            {
                agent.ExplorationRate = ExplorationRate;
                int batch = Math.Min(EpisodesPerUpdate, target - done);
                var hook = AfterEpisode;
                var finished = await Task.Run(() => PlayEpisodes(agent, batch, token, hook), CancellationToken.None);
                if (generation != _generation) return;   // Reset, an edit or leaving the lab ended this run
                foreach (var (steps, reached) in finished) Record(steps, reached);
                done += finished.Count;
                if (done < target && !token.IsCancellationRequested && UpdateInterval - sinceUpdate.Elapsed is { Ticks: > 0 } wait)
                {
                    try { await Task.Delay(wait, token); }
                    catch (OperationCanceledException) { }
                    if (generation != _generation) return;
                }
                Refresh();
                sinceUpdate.Restart();
            }
        }
        finally
        {
            if (generation == _generation)
            {
                // A rate set during the last batch, or while Stop took effect, has not reached the agent yet.
                _agent.ExplorationRate = ExplorationRate;
                IsRunning = false;
                EndRun();
            }
        }
    }

    // On the worker: whole episodes, checking Stop before each one.
    private static List<(int Steps, bool Reached)> PlayEpisodes(QLearningAgent agent, int count, CancellationToken token, Action<int>? after)
    {
        var finished = new List<(int, bool)>(count);
        while (finished.Count < count && !token.IsCancellationRequested)
        {
            int steps = agent.StepsThisEpisode;   // an episode Step had begun is finished here
            StepResult result;
            do
            {
                result = agent.Step();
                steps++;
            }
            while (!result.EpisodeEnded);
            finished.Add((steps, result.To == agent.World.Goal));
            after?.Invoke(agent.Episodes);
        }
        return finished;
    }

    public override void Reset()
    {
        // A run under way ends, but its screens are compared only once Reset's own result has been reported: Reset is
        // a direct action and lights each changed screen once, never for the stopped run's half-way state.
        var run = DetachRun();
        _quiet = true;
        try
        {
            Tool = MazeTool.Wall;
            GoalReward = DefaultGoalReward;
            StepCost = DefaultStepCost;
            ExplorationRate = DefaultExplorationRate;
            Discount = DefaultDiscount;
            Seed = 1;
        }
        finally { _quiet = false; }
        _walls.Clear();
        _walls.UnionWith(DefaultWalls);
        _start = DefaultStart;
        _goal = DefaultGoal;
        _refusal = null;
        _changed = false;
        LastStepText = "";
        NewAgent();
        Refresh();
        run?.Dispose();
    }

    public override void Deactivate() => StopRun();

    protected override void OnSeedEdited()
    {
        if (!_quiet) Restart();
    }

    partial void OnGoalRewardChanged(double value)
    {
        double fitted = Fit(value, MinGoalReward, MaxGoalReward, DefaultGoalReward);
        if (!fitted.Equals(value)) { GoalReward = fitted; return; }
        if (!_quiet) Restart();
    }

    partial void OnStepCostChanged(double value)
    {
        double fitted = Fit(value, MinStepCost, MaxStepCost, DefaultStepCost);
        if (!fitted.Equals(value)) { StepCost = fitted; return; }
        if (!_quiet) Restart();
    }

    partial void OnDiscountChanged(double value)
    {
        double fitted = Fit(value, MinDiscount, MaxDiscount, DefaultDiscount);
        if (!fitted.Equals(value)) { Discount = fitted; return; }
        if (!_quiet) Restart();
    }

    // The exploration rate does not clear learning. During a run it reaches the agent before the next batch.
    partial void OnExplorationRateChanged(double value)
    {
        double fitted = Fit(value, MinExplorationRate, MaxExplorationRate, DefaultExplorationRate);
        if (!fitted.Equals(value)) { ExplorationRate = fitted; return; }
        if (!IsRunning) _agent.ExplorationRate = fitted;
    }

    partial void OnToolChanged(MazeTool value)
    {
        if (!Enum.IsDefined(value)) Tool = MazeTool.Wall;
    }

    // Not a number gives the default; otherwise clamped and rounded to two decimals (slider steps can land on 0.30000000000000004).
    private static double Fit(double value, double min, double max, double fallback) =>
        double.IsNaN(value) ? fallback : Math.Round(Math.Clamp(value, min, max), 2, MidpointRounding.AwayFromZero);

    /// <summary>A change that clears learning: any run ends, a fresh agent starts, and the message says so.</summary>
    private void Restart()
    {
        var run = DetachRun();   // as in Reset: the edit lights each changed screen once, for its own result
        _refusal = null;
        _changed = true;
        LastStepText = "";
        NewAgent();
        Refresh();
        run?.Dispose();
    }

    private void Refuse(string why)
    {
        _refusal = why;
        UpdateMessage();
        ReportScreens();
    }

    /// <summary>Ends the generation: a run under way stops, and whatever its worker returns is thrown away.</summary>
    private void StopRun() => DetachRun()?.Dispose();

    /// <summary>Ends the generation and returns the run's screen scope (null when no run), still open, for the caller
    /// to close once its own result is reported.</summary>
    private IDisposable? DetachRun()
    {
        // The generation ends first: cancelling may run the run's continuation at once, and it must see that it is over.
        _generation++;
        if (RunCommand.IsRunning) RunCancelCommand.Execute(null);
        IsRunning = false;
        var run = _run;
        _run = null;
        return run;
    }

    private void EndRun()
    {
        var run = _run;
        _run = null;
        run?.Dispose();
    }

    private void NewAgent()
    {
        var world = new GridWorld(Columns, Rows, _walls, _start, _goal);
        _reachable = world.GoalReachable;
        _agent = new QLearningAgent(world, Settings, Seed);
        _steps = [];
        _lastSteps = 0;
        _lastReached = false;
    }

    private void Record(int steps, bool reached)
    {
        _steps.Add((_steps.Count + 1, steps));
        _lastSteps = steps;
        _lastReached = reached;
    }

    private void UpdateMessage() =>
        Message = _refusal ?? (!_reachable ? UnreachableMessage : _changed ? ChangedMessage : null);

    // Only called while the agent is not being changed by a worker.
    private void Refresh()
    {
        var agent = _agent;
        var world = agent.World;
        var list = new List<MazeSquare>(Columns * Rows);
        for (int row = 0; row < Rows; row++)
        for (int column = 0; column < Columns; column++)
        {
            var cell = new GridCell(column, row);
            bool wall = world.IsWall(cell), start = cell == world.Start, goal = cell == world.Goal, here = cell == agent.Position;
            double value = wall ? 0 : agent.Value(cell);
            string text = wall || goal ? "" : ValueString(value);
            var arrow = agent.Preferred(cell);
            list.Add(new MazeSquare(cell, wall, start, goal, here, value, text, arrow, Describe(cell, wall, start, goal, here, text, arrow)));
        }
        Squares = list;
        Episodes = agent.Episodes;
        EpisodesText = EpisodesLine(agent.Episodes, agent.StepsThisEpisode);
        StepsSeries = _steps.Count == 0 ? [] : [new LineSeries("steps", _steps.ToList())];
        UpdateMessage();
        ReportScreens();
    }

    private static string Describe(GridCell cell, bool wall, bool start, bool goal, bool here, string value, Move? arrow)
    {
        if (wall) return $"{SquareName(cell)}: wall";
        if (goal) return $"{SquareName(cell)}: goal";
        var parts = new List<string>();
        if (start) parts.Add("start");
        if (here) parts.Add("agent here");
        parts.Add($"value {value}");
        parts.Add(arrow is { } move ? $"prefers {MoveName(move)}" : "no single best move yet");
        return $"{SquareName(cell)}: {string.Join(", ", parts)}";
    }

    private string EpisodesLine(int episodes, int stepsSoFar)
    {
        string line = episodes == 0
            ? stepsSoFar == 0 ? "Episodes: 0. Nothing learned yet." : "Episodes: 0."
            : $"Episodes: {episodes.ToString("N0", CultureInfo.InvariantCulture)}. The last one took {Steps(_lastSteps)}" +
              (_lastReached ? "." : " and did not reach the goal.");
        return stepsSoFar == 0 ? line : $"{line} This one has taken {Steps(stepsSoFar)} so far.";
    }

    private static string Steps(int n) => $"{n.ToString("N0", CultureInfo.InvariantCulture)} {(n == 1 ? "step" : "steps")}";

    private string StepText(StepResult step)
    {
        string reward = $"reward {NumberText.Tick(step.Reward)}";
        string move = MoveName(step.Move);
        if (step.To == _agent.World.Goal)
            return $"Moved {move} from {SquareName(step.From).ToLowerInvariant()} into the goal: {reward}. The episode ends and the agent goes back to the start.";
        string text = step.To == step.From
            ? $"Tried to move {move} from {SquareName(step.From).ToLowerInvariant()} but hit a wall or the edge: {reward}."
            : $"Moved {move} from {SquareName(step.From).ToLowerInvariant()} to {SquareName(step.To).ToLowerInvariant()}: {reward}.";
        return step.EpisodeEnded ? $"{text} That was the episode's last step, so the agent goes back to the start." : text;
    }

    // Snapshots of what is shown: never the agent itself, which a worker may be changing.
    private void ReportScreens()
    {
        ReportScreen(ValuesScreen, new[] { Message ?? "", LastStepText }
            .Concat(Squares.Select(s => $"{s.Cell.Column},{s.Cell.Row},{s.IsWall},{s.IsStart},{s.IsGoal},{s.HasAgent},{s.ValueText},{s.Arrow}"))
            .ToArray());
        ReportScreen(EpisodesScreen, new[] { EpisodesText, _steps.Count.ToString(CultureInfo.InvariantCulture) });
    }
}
