using System.ComponentModel;
using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Vision;
using AIAnatomy.Presentation.Services;
using AIAnatomy.Presentation.Visuals;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIAnatomy.Presentation.Labs;

/// <summary>
/// Lesson 4.7's lab: the whole shipped digits network (<see cref="DigitTraining.Shipped"/>: 8 by 8 input, 16 then 32
/// filters, a fully connected layer of 128) reading one digit. The input is a digit from the bundled test set (Previous,
/// Next, or a digit 0 to 9, which is the keyboard path) or the learner's drawing, made into an 8 by 8 image by
/// <see cref="DigitImage.FromStrokes"/> as the training digits were made. One forward pass (<see cref="DigitNet.Forward"/>)
/// gives the layer 1 maps (16 of 8 by 8, after ReLU), the layer 2 maps (32 of 4 by 4, after ReLU), the fully connected
/// layer and the probabilities for 0 to 9, all worked out again at once on the calling thread.
/// <para>An empty pad gives <see cref="EmptyMessage"/> and a drawing whose ink is a dot (<see cref="IsADot"/>) gives
/// <see cref="DotMessage"/>, each instead of a prediction: the shipped network reads a lone dot as a 4 (0.37), which would
/// be a reading of something that is not a digit.</para>
/// <para>Train it again trains a fresh network of the same shape (<see cref="DigitTraining.Retrain"/>) for
/// <see cref="DigitTraining.RetrainSteps"/> steps with the seed; when it completes, its accuracy on the test digits is
/// measured and it replaces the shipped network in the lab until Reset. Screens: "maps", "probabilities", "training".</para>
/// </summary>
public sealed partial class DigitNetworkLabViewModel : LabViewModel
{
    public const string Id = "DigitNetworkLab";
    public const int ImageSize = 8, Positions = ImageSize * ImageSize;

    /// <summary>A drawing whose points all fit in a box smaller than this, in pad units (a tenth of the pad), both across and
    /// down, is a dot, not a digit. It is <see cref="DigitImage.MinMark"/>: marks that small are not enlarged when the drawing
    /// is made into 8 by 8, so they stay a small blob in the middle that looks like no digit.</summary>
    public const double MinDigitSize = DigitImage.MinMark;

    public const string EmptyMessage = "Draw a digit first, or pick a sample.";
    public const string DotMessage = "That's too small to read as a digit: draw it bigger, or pick a sample.";
    public const string LoadingMessage = "Loading the digits and the network…";
    public const string LoadFailedMessage = "The digits network could not be loaded because of an unexpected problem. Reinstalling the app may fix it.";

    public const string DrawingCaption =
        "Your drawing, made into an 8 by 8 image the way the test digits were made: the ink counted in 4 by 4 blocks of a 32 by 32 picture.";

    /// <summary>The digits 0 to 9, for the digit picker.</summary>
    public static IReadOnlyList<int> Digits { get; } = Enumerable.Range(0, 10).ToArray();

    /// <summary>Row and column names, 1 to 8.</summary>
    public static IReadOnlyList<string> PositionLabels { get; } = Enumerable.Range(1, ImageSize).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray();

    private readonly FamilyModels _family;
    private readonly Task _ready;
    private DigitSet? _test, _train;
    private DigitNet? _shipped, _current;
    private double[]? _pixels;

    private int _trainGeneration;
    private IDisposable? _run;
    private DigitNet? _trainingNet;
    private bool _quiet;
    private int _retrainSteps = DigitTraining.RetrainSteps;

    public DigitNetworkLabViewModel(FamilyModels family)
    {
        _family = family ?? throw new ArgumentNullException(nameof(family));
        Runner = new RetrainRunner(CreateTrainer);
        Runner.PropertyChanged += OnRunnerChanged;
        _ready = LoadAsync();
    }

    public override string LabId => Id;

    /// <summary>Loading the digits and the network.</summary>
    public override Task WorkCompleted => _ready;

    /// <summary>Train it again, with <see cref="DigitTraining.Retrain"/>.</summary>
    public RetrainRunner Runner { get; }

    /// <summary>Steps for train it again: 1 to <see cref="DigitTraining.RetrainSteps"/> (a test lowers it).</summary>
    public int RetrainSteps
    {
        get => _retrainSteps;
        set => _retrainSteps = Math.Clamp(value, 1, DigitTraining.RetrainSteps);
    }

    /// <summary>A test hook, called on the worker after each training step with the steps taken.</summary>
    public Action<int>? AfterTrainingStep { get; set; }

    /// <summary>A test hook, called on a worker after a completed run, before its network is measured.</summary>
    public Action? BeforeMeasuringTrained { get; set; }

    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private bool isReady;

    /// <summary>True from Train it again until its result is shown (the run and then the measurement of its network).</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(TrainAgainCommand))]
    private bool isTraining;

    private bool CanTrain => IsReady && !IsTraining;

    /// <summary>While loading, what is happening; if loading failed, why. Null once ready.</summary>
    [ObservableProperty] private string? loadMessage = LoadingMessage;

    // ---- the input ----

    /// <summary>The test digit on show (or last shown, while a drawing is in use), from 0.</summary>
    public int SampleIndex { get; private set; }

    /// <summary>How many digits the test set holds (0 until loaded).</summary>
    public int SampleCount => _test?.Count ?? 0;

    /// <summary>Which digit the test digit is.</summary>
    [ObservableProperty] private int sampleLabel;

    /// <summary>True while the input is the drawing pad (even an empty one); false while it is a test digit.</summary>
    [ObservableProperty] private bool usingDrawing;

    /// <summary><see cref="EmptyMessage"/> or <see cref="DotMessage"/> when there is nothing to read; otherwise null.</summary>
    [ObservableProperty] private string? message;

    /// <summary>The input image, [row, column], values 0 to 1; null for an empty pad or until loaded.</summary>
    [ObservableProperty] private double[,]? image;

    [ObservableProperty] private string imageCaption = "";

    // ---- the network's reading ----

    /// <summary>The layer 1 feature maps after ReLU: 16 of 8 by 8 (empty when there is no prediction).</summary>
    [ObservableProperty] private IReadOnlyList<double[,]> layer1Maps = [];

    /// <summary>The layer 2 feature maps after ReLU: 32 of 4 by 4 (empty when there is no prediction).</summary>
    [ObservableProperty] private IReadOnlyList<double[,]> layer2Maps = [];

    /// <summary>How many of the fully connected layer's numbers are above zero after ReLU.</summary>
    [ObservableProperty] private int hiddenActive;

    [ObservableProperty] private string hiddenText = "";

    /// <summary>The probability of each digit 0 to 9 (empty when there is no prediction).</summary>
    [ObservableProperty] private IReadOnlyList<double> probabilities = [];

    /// <summary>The probabilities as bars labelled "0" to "9", in that order.</summary>
    [ObservableProperty] private IReadOnlyList<LabeledValue> bars = [];

    /// <summary>The most probable digit, or null when there is no prediction.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPrediction), nameof(HighlightedBar))] private int? predictedDigit;

    public bool HasPrediction => PredictedDigit != null;

    /// <summary>The bar of the most probable digit, or null.</summary>
    public string? HighlightedBar => PredictedDigit?.ToString(CultureInfo.InvariantCulture);

    /// <summary>"It reads this as a 7 (91%).", or the message when there is nothing to read.</summary>
    [ObservableProperty] private string predictionText = "";

    /// <summary>For a test digit, its label and whether the reading is right; empty for a drawing.</summary>
    [ObservableProperty] private string answerText = "";

    // ---- accuracy and training ----

    /// <summary>The shipped network's test accuracy, recorded in its model file when it was built.</summary>
    [ObservableProperty] private double recordedAccuracy = double.NaN;

    /// <summary>"Recorded when it was built: right on 1,705 of 1,740 test digits (98.0%)."</summary>
    [ObservableProperty] private string recordedAccuracyText = "";

    /// <summary>True once a completed run's network has replaced the shipped one in the lab (Reset brings it back).</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NetworkName))] private bool usingTrainedNetwork;

    /// <summary>The trained network's accuracy on the test digits, measured when its run completed; null before.</summary>
    [ObservableProperty] private double? trainedAccuracy;

    /// <summary>The run's progress line (the runner's), cleared when a run is abandoned.</summary>
    [ObservableProperty] private string trainingStatus = "";

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasTrainingSeries))] private IReadOnlyList<LineSeries> trainingSeries = [];

    public bool HasTrainingSeries => TrainingSeries.Count > 0;

    /// <summary>What a completed run gave; empty before one.</summary>
    [ObservableProperty] private string trainingResult = "";

    /// <summary>The network the lab uses now; for tests.</summary>
    internal DigitNet? CurrentNetwork => _current;

    /// <summary>Which network reads the input.</summary>
    public string NetworkName => UsingTrainedNetwork ? "the network you trained" : "the shipped network";

    public string TrainingNote =>
        $"Train it again trains a new network of the same shape from random starting numbers, with the seed, for {N0(DigitTraining.RetrainSteps)} steps " +
        $"(about 11 to 18 seconds on the PC that built the app). The shipped one was trained for {N0(DigitTraining.ShippedSteps)} steps, twice as long, " +
        "so expect your network to be right a little less often: in checks with seeds 1, 2 and 3 a retrained network was right on 96.5% to 97.1% of the test digits.";

    /// <summary>"It reads this as a 7 (91%)." The percentage is whole, but never rounded up to 100% or down to 0%.</summary>
    public static string PredictionFor(int digit, double probability)
    {
        double percent = probability * 100;
        string chance = Math.Round(percent) switch
        {
            >= 100 when probability < 1 => "over 99%",
            <= 0 when probability > 0 => "under 1%",
            var p => $"{p.ToString("0", CultureInfo.InvariantCulture)}%",
        };
        string article = digit == 8 ? "an" : "a";
        return $"It reads this as {article} {digit} ({chance}).";
    }

    /// <summary>Whether the drawing's ink is a dot: it has points, and they all fit in a box smaller than
    /// <see cref="MinDigitSize"/> both across and down.</summary>
    public static bool IsADot(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        double x0 = double.MaxValue, x1 = double.MinValue, y0 = double.MaxValue, y1 = double.MinValue;
        bool any = false;
        foreach (var stroke in strokes)
        {
            if (stroke is null) continue;
            foreach (var (x, y) in stroke)
            {
                any = true;
                x0 = Math.Min(x0, x); x1 = Math.Max(x1, x);
                y0 = Math.Min(y0, y); y1 = Math.Max(y1, y);
            }
        }
        return any && x1 - x0 < MinDigitSize && y1 - y0 < MinDigitSize;
    }

    [RelayCommand]
    public void Next()
    {
        if (_test is null) return;
        ShowSample((SampleIndex + 1) % _test.Count);
    }

    [RelayCommand]
    public void Previous()
    {
        if (_test is null) return;
        ShowSample((SampleIndex - 1 + _test.Count) % _test.Count);
    }

    /// <summary>Shows the next test digit (after the one last shown, going round) that is a <paramref name="digit"/>.</summary>
    [RelayCommand]
    public void ShowDigit(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digit, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);
        if (_test is null) return;
        for (int step = 1; step <= _test.Count; step++)
        {
            int i = (SampleIndex + step) % _test.Count;
            if (_test.Label(i) == digit) { ShowSample(i); return; }
        }
    }

    /// <summary>Uses the drawing as the input. No ink gives <see cref="EmptyMessage"/>, a dot <see cref="DotMessage"/>,
    /// each instead of a prediction; anything else is made into 8 by 8 and read.</summary>
    public void UseDrawing(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        if (_test is null) return;
        var pixels = DigitImage.FromStrokes(strokes, ImageSize);
        UsingDrawing = true;
        SampleLabelKnown = false;
        ImageCaption = DrawingCaption;
        if (pixels is null)
        {
            _pixels = null;
            Image = null;
            ImageCaption = "";
            ShowNoPrediction(EmptyMessage);
        }
        else if (IsADot(strokes))
        {
            _pixels = pixels;
            Image = ToGrid(pixels, ImageSize);
            ShowNoPrediction(DotMessage);
        }
        else
        {
            _pixels = pixels;
            Image = ToGrid(pixels, ImageSize);
            Message = null;
            Read();
        }
        ReportScreens();
    }

    public override void Reset()
    {
        var run = DetachRun();
        _quiet = true;
        try { Seed = 1; }
        finally { _quiet = false; }
        if (IsReady)
        {
            _current = _shipped;
            UsingTrainedNetwork = false;
            TrainedAccuracy = null;
            TrainingResult = "";
            TrainingStatus = "";
            TrainingSeries = [];
            ShowSample(0);
        }
        run?.Dispose();
    }

    public override void Deactivate() => DetachRun()?.Dispose();

    protected override void OnSeedEdited()
    {
        if (_quiet) return;
        var run = DetachRun();   // a new seed abandons a run under way
        ReportScreens();
        run?.Dispose();
    }

    /// <summary>Ends a run early; nothing it trained is used.</summary>
    [RelayCommand]
    public void StopTraining() => Runner.Stop();

    /// <summary>Trains a fresh network (the Retrain config) with the seed. When it completes, its accuracy on the test
    /// digits is measured and shown, and it replaces the shipped network in the lab. The screens light once, at the end.</summary>
    [RelayCommand(CanExecute = nameof(CanTrain), AllowConcurrentExecutions = true)]
    private async Task TrainAgainAsync()
    {
        if (!CanTrain || Runner.IsRunning) return;
        int generation = ++_trainGeneration;
        int seed = Seed;
        _run?.Dispose();   // never leave an older scope open: the screens would stop lighting
        _run = BeginRun();
        IsTraining = true;
        TrainingResult = "";   // the last run's result is not this run's (final review M13)
        try
        {
            await Runner.StartAsync(seed);
            if (generation != _trainGeneration) return;   // Reset, a new seed or leaving the lab ended this run
            var net = _trainingNet;
            if (Runner.Finished is { } trainer && net != null)
            {
                double seconds = Runner.Elapsed.TotalSeconds;
                var test = _test!;
                var hook = BeforeMeasuringTrained;
                double accuracy = await Task.Run(() =>
                {
                    hook?.Invoke();
                    return DigitTrainer.Accuracy(net, test);
                });
                if (generation != _trainGeneration) return;
                _current = net;
                UsingTrainedNetwork = true;
                TrainedAccuracy = accuracy;
                TrainingResult = $"Trained again with seed {seed}, for {N0(trainer.StepsTaken)} steps in {Math.Max(1, (int)Math.Round(seconds))} s. " +
                                 $"{RightOn(accuracy, test.Count, capital: true)}. The lab now reads the digits with this network; Reset brings back the shipped one.";
                if (HasPrediction) Read();
            }
            ReportScreens();
        }
        finally
        {
            if (generation == _trainGeneration)
            {
                IsTraining = false;
                var run = _run;
                _run = null;
                run?.Dispose();
            }
        }
    }

    private bool SampleLabelKnown { get; set; }

    private void ShowSample(int index)
    {
        var set = _test!;
        SampleIndex = index;
        OnPropertyChanged(nameof(SampleIndex));
        SampleLabel = set.Label(index);
        SampleLabelKnown = true;
        _pixels = set.Pixels(index);
        Image = ToGrid(_pixels, ImageSize);
        ImageCaption = $"Test digit {N0(index + 1)} of {N0(set.Count)}: a {SampleLabel}.";
        UsingDrawing = false;
        Message = null;
        Read();
        ReportScreens();
    }

    /// <summary>Runs the input through the network in use and shows every stage.</summary>
    private void Read()
    {
        var net = _current!;
        var trace = net.Forward(Tensor.FromArray((double[])_pixels!.Clone(), 1, 1, ImageSize, ImageSize));
        Layer1Maps = Maps(trace.Conv1);
        Layer2Maps = Maps(trace.Conv2);
        int units = trace.Hidden.Shape[1];
        HiddenActive = trace.Hidden.Data.Count(v => v > 0);
        HiddenText = $"The fully connected layer: {HiddenActive} of the {units} numbers are above zero for this digit; the rest were set to 0.";
        var p = trace.Probabilities(0);
        int best = 0;
        for (int j = 1; j < p.Length; j++)
            if (p[j] > p[best]) best = j;   // the first of any tie, as the accuracy count does
        Probabilities = p;
        Bars = p.Select((v, d) => new LabeledValue(d.ToString(CultureInfo.InvariantCulture), v)).ToArray();
        PredictedDigit = best;
        PredictionText = PredictionFor(best, p[best]);
        AnswerText = !SampleLabelKnown ? ""
            : best == SampleLabel ? $"The test digit is labelled {SampleLabel}, so this reading is right."
            : $"The test digit is labelled {SampleLabel}, so this reading is a mistake.";
    }

    private void ShowNoPrediction(string message)
    {
        Message = message;
        Layer1Maps = [];
        Layer2Maps = [];
        HiddenActive = 0;
        HiddenText = "";
        Probabilities = [];
        Bars = [];
        PredictedDigit = null;
        PredictionText = message;
        AnswerText = "";
    }

    private static IReadOnlyList<double[,]> Maps(Tensor t)
    {
        int count = t.Shape[1], size = t.Shape[2];
        var maps = new double[count][,];
        for (int m = 0; m < count; m++)
        {
            var grid = new double[size, size];
            int start = m * size * size;
            for (int r = 0; r < size; r++)
                for (int c = 0; c < size; c++) grid[r, c] = t.Data[start + r * size + c];
            maps[m] = grid;
        }
        return maps;
    }

    private async Task LoadAsync()
    {
        try
        {
            var testTask = _family.DigitTestSetAsync();
            var trainTask = _family.DigitTrainSetAsync();
            var netTask = _family.DigitsAsync();
            await Task.WhenAll(testTask, trainTask, netTask);
            var test = testTask.Result;
            var train = trainTask.Result;
            var (net, info) = netTask.Result;
            if (test.ImageSize != ImageSize || train.ImageSize != ImageSize || net.Config.ImageSize != ImageSize || test.Count == 0 || train.Count == 0)
                throw new InvalidDataException($"The digits that come with the app are not {ImageSize} by {ImageSize} images. Reinstalling the app puts them back.");
            _test = test;
            _train = train;
            _shipped = _current = net;
            OnPropertyChanged(nameof(SampleCount));
            RecordedAccuracy = info.Results != null && info.Results.TryGetValue("testAccuracy", out var recorded) ? recorded : double.NaN;
            RecordedAccuracyText = double.IsFinite(RecordedAccuracy)
                ? $"Recorded when it was built: {RightOn(RecordedAccuracy, test.Count, capital: false)}."
                : "No test accuracy was recorded when it was built.";
            LoadMessage = null;
            IsReady = true;
            ShowSample(0);   // the baselines: nothing lights for loading
        }
        catch (InvalidDataException e)
        {
            LoadMessage = e.Message;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Anything else must not leave "Loading…" on screen for ever.
            ErrorLog.ReportBackground(e);
            LoadMessage = LoadFailedMessage;
        }
    }

    /// <summary>"right on 1,705 of 1,740 test digits (98.0%)" for an accuracy measured on <paramref name="count"/> digits.</summary>
    private static string RightOn(double accuracy, int count, bool capital)
    {
        int right = (int)Math.Round(accuracy * count);
        string text = $"right on {N0(right)} of {N0(count)} test digits ({(accuracy * 100).ToString("0.0", CultureInfo.InvariantCulture)}%)";
        return capital ? char.ToUpperInvariant(text[0]) + text[1..] : text;
    }

    private ISteppedTrainer CreateTrainer(int seed)
    {
        var net = new DigitNet(DigitTraining.Retrain, new SeededRandom(seed));
        var trainer = new DigitTrainer(net, _train!, RetrainSteps, seed);
        _trainingNet = net;
        return AfterTrainingStep is { } hook ? new HookedTrainer(trainer, hook) : trainer;
    }

    private void OnRunnerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RetrainRunner.StatusText)) TrainingStatus = Runner.StatusText;
        else if (e.PropertyName == nameof(RetrainRunner.LossSeries)) TrainingSeries = Runner.LossSeries;
        else if (e.PropertyName == nameof(RetrainRunner.IsRunning)) TrainAgainCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Ends the training generation: a run under way is abandoned (nothing it trained is used) and its progress
    /// cleared. Returns the run's screen scope, still open, for the caller to close once its own result is reported.</summary>
    private IDisposable? DetachRun()
    {
        _trainGeneration++;
        IsTraining = false;
        if (Runner.IsRunning)
        {
            Runner.Abandon();
            TrainingStatus = "";
            TrainingSeries = [];
        }
        var run = _run;
        _run = null;
        return run;
    }

    private void ReportScreens()
    {
        if (!IsReady) return;
        var maps = new List<object> { ImageCaption, Message ?? "", NetworkName };
        if (_pixels != null) maps.AddRange(_pixels.Cast<object>());
        foreach (var m in Layer1Maps.Concat(Layer2Maps)) maps.Add(m.Cast<double>().ToArray());
        ReportScreen("maps", maps.ToArray());
        ReportScreen("probabilities", new object[] { PredictionText, AnswerText, NetworkName }.Concat(Probabilities.Cast<object>()).ToArray());
        ReportScreen("training", new object[] { TrainingResult, TrainingStatus });
    }

    private static double[,] ToGrid(IReadOnlyList<double> values, int size)
    {
        var grid = new double[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++) grid[r, c] = values[r * size + c];
        return grid;
    }

    private static string N0(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Passes each step to the trainer, then tells the test hook how many steps have been taken.</summary>
    private sealed class HookedTrainer(ISteppedTrainer inner, Action<int> after) : ISteppedTrainer
    {
        public int TotalSteps => inner.TotalSteps;
        public int StepsTaken => inner.StepsTaken;

        public double Step()
        {
            double loss = inner.Step();
            after(inner.StepsTaken);
            return loss;
        }
    }
}
