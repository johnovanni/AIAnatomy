using System.Globalization;
using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Lesson 4.10's lab: noise added to the spiral step by step (forward), the shipped network's guess of that noise
/// at one step (prediction), and taking the noise out again with 100, 50, 20, 10 or 5 steps (reverse). Sampling 500 points
/// for 100 steps takes seconds in a Debug build, so only the tests that compare the real settings with the shipped sampler
/// use 500 points; the rest give the lab fewer (an internal constructor), which changes no rule they check.</summary>
public sealed class DiffusionStepsLabTests
{
    /// <summary>Points for the tests that check behaviour, not the shipped result.</summary>
    private const int FewPoints = 40;

    private static readonly IReadOnlyList<(double X, double Y)> Spiral = Shapes.Presets[0].Points;

    /// <summary>A lab with the spiral loaded and its first (100-step) run worked out. With no SynchronizationContext every
    /// update is applied where it is made, so nothing needs pumping.</summary>
    private static async Task<DiffusionStepsLabViewModel> NewLab(int points = FewPoints)
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new DiffusionStepsLabViewModel(new FamilyModels(TestEnvironment.Content), points);
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return lab;
    }

    private static Task<(DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info)> Shipped() =>
        new FamilyModels(TestEnvironment.Content).DiffusionAsync("Spiral");

    private static IReadOnlyList<(double X, double Y)> Points(double[,] frame) =>
        Enumerable.Range(0, frame.GetLength(0)).Select(i => (frame[i, 0], frame[i, 1])).ToList();

    private static double[] Flat(double[,] frame) => frame.Cast<double>().ToArray();

    private static void AssertSameFrames(IReadOnlyList<double[,]> expected, IReadOnlyList<double[,]> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (int k = 0; k < expected.Count; k++) Assert.Equal(Flat(expected[k]), Flat(actual[k]));
    }

    private static string F3(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static NoiseSchedule WithSteps(NoiseSchedule schedule, int steps) => steps == schedule.Steps ? schedule : schedule.Respaced(steps);

    // ---- reverse, with the real settings ----

    [Fact]
    public async Task HundredSteps_IsTheShippedSampler_AndFiveSteps_LandFurtherFromTheShape()
    {
        // The real settings: the shipped spiral network, 500 points, seed 1.
        var lab = await NewLab(DiffusionStepsLabViewModel.SampleCount);
        Assert.Equal("DiffusionStepsLab", DiffusionStepsLabViewModel.Id);
        Assert.Equal(DiffusionStepsLabViewModel.Id, lab.LabId);
        Assert.Equal(new[] { 100, 50, 20, 10, 5 }, DiffusionStepsLabViewModel.StepChoices);
        Assert.Equal(500, DiffusionStepsLabViewModel.SampleCount);
        var (net, schedule, _, _) = await Shipped();

        // It opens on 100 steps: exactly what lesson 4.9's lab and the shipped sampler make, shown at the finished cloud.
        var hundred = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(1));
        Assert.Equal(100, lab.SampleSteps);
        AssertSameFrames(hundred, lab.Frames);
        Assert.Equal(100, lab.MaxFrame);
        Assert.Equal(100, lab.Frame);
        Assert.Equal(Points(hundred[^1]), lab.CurrentPoints);
        double d100 = Shapes.MeanDistanceToShape(Points(hundred[^1]), Spiral);
        double c100 = Shapes.Coverage(Points(hundred[^1]), Spiral, DiffusionStepsLabViewModel.CoverageRadius);
        Assert.Equal(DiffusionStepsLabViewModel.ResultFor(100, d100, c100), lab.ResultText);
        Assert.Contains($"Average distance from the shape: {F3(d100)} (lower is better).", lab.ResultText);

        // Five steps: the respaced sampler (Nichol and Dhariwal 2021), and further from the spiral.
        await lab.ChooseStepsAsync(5);
        var five = DiffusionSampler.Sample(net, schedule.Respaced(5), 500, new SeededRandom(1));
        AssertSameFrames(five, lab.Frames);
        Assert.Equal(5, lab.MaxFrame);
        Assert.Equal(5, lab.Frame);
        double d5 = Shapes.MeanDistanceToShape(Points(five[^1]), Spiral);
        double c5 = Shapes.Coverage(Points(five[^1]), Spiral, DiffusionStepsLabViewModel.CoverageRadius);
        Assert.Equal(DiffusionStepsLabViewModel.ResultFor(5, d5, c5), lab.ResultText);
        Assert.True(d5 > d100, $"5 steps: {d5}; 100 steps: {d100}");
        Assert.Contains($"100 steps: {F3(d100)}", lab.MeasuredText);
        Assert.Contains($"5 steps: {F3(d5)}", lab.MeasuredText);
    }

    // ---- forward ----

    [Fact]
    public async Task ForwardPoints_AreAddNoise_WithTheSeed_AtEachStep()
    {
        var lab = await NewLab();
        var (_, schedule, _, _) = await Shipped();
        Assert.Equal(Spiral, lab.TargetPoints);
        Assert.Equal(DiffusionStepsLabViewModel.DefaultForwardStep, lab.ForwardStep);
        Assert.Equal(100, lab.MaxForwardStep);
        foreach (int seed in new[] { 1, 4 })
        {
            lab.Seed = seed;
            await lab.WorkCompleted;
            foreach (int t in new[] { 0, 1, 37, 100 })
            {
                lab.ForwardStep = t;
                var (noisy, _) = DiffusionSampler.AddNoise(Spiral, t, schedule, new SeededRandom(seed));
                Assert.Equal(Points(noisy), lab.ForwardPoints);
            }
        }
        lab.ForwardStep = 400;   // clamped
        Assert.Equal(100, lab.ForwardStep);
        lab.ForwardStep = -2;
        Assert.Equal(0, lab.ForwardStep);
    }

    [Fact]
    public async Task ForwardText_AndTheScheduleChart_FollowTheStep()
    {
        var lab = await NewLab();
        var (_, schedule, _, _) = await Shipped();
        lab.ForwardStep = 0;
        Assert.Equal("Step 0 of 100: the shape itself. No noise has been added yet.", lab.ForwardText);
        lab.ForwardStep = 40;
        double ab = schedule.AlphaBar(40);
        Assert.Equal($"Step 40 of 100: ᾱ = {F3(ab)}, so each point is {F3(Math.Sqrt(ab))} × its place on the shape plus " +
                     $"{F3(Math.Sqrt(1 - ab))} × its noise. This step's β is {schedule.Beta(40).ToString("0.0000", CultureInfo.InvariantCulture)}.",
                     lab.ForwardText);

        Assert.Equal(3, lab.ScheduleSeries.Count);
        var alphaBar = lab.ScheduleSeries[0];
        var beta = lab.ScheduleSeries[1];
        var marker = lab.ScheduleSeries[2];
        Assert.Equal(Enumerable.Range(0, 101).Select(t => ((double)t, schedule.AlphaBar(t))), alphaBar.Points);
        Assert.Equal(Enumerable.Range(1, 100).Select(t => ((double)t, schedule.Beta(t))), beta.Points);
        Assert.Equal(new[] { (40.0, 0.0), (40.0, ab) }, marker.Points);   // a line up from the axis to ᾱ at the step shown
        Assert.Contains("ᾱ", alphaBar.Name);
        Assert.Contains("β", beta.Name);
        Assert.Contains("40", marker.Name);
    }

    // ---- prediction ----

    [Fact]
    public async Task Arrows_AreTheTrueNoise_AndTheNetworksPrediction_AtTheStepShown()
    {
        var lab = await NewLab();
        var (net, schedule, _, _) = await Shipped();
        Assert.Equal(Enumerable.Range(0, 20).Select(i => i * 50), lab.MarkedIndices);
        foreach (int t in new[] { 1, 20, 90 })
        {
            lab.ForwardStep = t;
            var (noisy, noise) = DiffusionSampler.AddNoise(Spiral, t, schedule, new SeededRandom(1));
            var marked = lab.MarkedIndices;
            var input = new double[marked.Count * 2];
            for (int k = 0; k < marked.Count; k++) { input[2 * k] = noisy[marked[k], 0]; input[2 * k + 1] = noisy[marked[k], 1]; }
            var guess = net.Forward(Tensor.FromArray(input, marked.Count, 2), Enumerable.Repeat(t, marked.Count).ToArray(), schedule.OriginalSteps).Data;

            Assert.Equal(20, lab.TrueNoiseArrows.Count);
            Assert.Equal(20, lab.PredictedNoiseArrows.Count);
            double squared = 0, baseline = 0;
            for (int k = 0; k < marked.Count; k++)
            {
                var from = (noisy[marked[k], 0], noisy[marked[k], 1]);
                Assert.Equal(from, lab.TrueNoiseArrows[k].From);
                Assert.Equal(from, lab.PredictedNoiseArrows[k].From);
                Assert.Equal((from.Item1 + noise[marked[k], 0], from.Item2 + noise[marked[k], 1]), lab.TrueNoiseArrows[k].To);
                Assert.Equal((from.Item1 + guess[2 * k], from.Item2 + guess[2 * k + 1]), lab.PredictedNoiseArrows[k].To);
                squared += Math.Pow(noise[marked[k], 0] - guess[2 * k], 2) + Math.Pow(noise[marked[k], 1] - guess[2 * k + 1], 2);
                baseline += Math.Pow(noise[marked[k], 0], 2) + Math.Pow(noise[marked[k], 1], 2);
            }
            double mse = squared / (2 * marked.Count);
            Assert.Equal(mse, lab.PredictionError, 12);
            Assert.Equal(DiffusionStepsLabViewModel.PredictionTextFor(t, mse, baseline / (2 * marked.Count)), lab.PredictionText);
            Assert.Contains($"{F3(mse)}", lab.PredictionText);
        }
    }

    [Fact]
    public async Task AtStep0_ThereIsNoNoise_SoNothingToPredict()
    {
        var lab = await NewLab();
        lab.ForwardStep = 0;
        Assert.Empty(lab.TrueNoiseArrows);
        Assert.Empty(lab.PredictedNoiseArrows);
        Assert.True(double.IsNaN(lab.PredictionError));
        Assert.Equal(DiffusionStepsLabViewModel.NothingToPredictMessage, lab.PredictionText);
        lab.ForwardStep = 1;
        Assert.Equal(20, lab.TrueNoiseArrows.Count);
    }

    // ---- reverse, behaviour ----

    [Fact]
    public async Task EachChoice_IsTheRespacedSampler_AndIsKeptForAnInstantReturn()
    {
        var lab = await NewLab();
        var (net, schedule, _, _) = await Shipped();
        var hundred = lab.Frames;
        foreach (int steps in new[] { 50, 20, 10, 5 })
        {
            await lab.ChooseStepsAsync(steps);
            Assert.Equal(steps, lab.SampleSteps);
            AssertSameFrames(DiffusionSampler.Sample(net, WithSteps(schedule, steps), FewPoints, new SeededRandom(1)), lab.Frames);
            Assert.Equal(steps, lab.Frame);
            Assert.Null(lab.ReverseNote);
        }
        var ten = lab.Frames;
        // Back to 100: kept from the first run, so shown at once (no sampling, the same frames).
        var choosing = lab.ChooseStepsAsync(100);
        Assert.True(choosing.IsCompleted);
        Assert.Same(hundred, lab.Frames);
        await lab.ChooseStepsAsync(5);
        Assert.Same(ten, lab.Frames);
        foreach (int steps in DiffusionStepsLabViewModel.StepChoices) Assert.Contains($"{steps} steps: ", lab.MeasuredText);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => lab.ChooseStepsAsync(7));
    }

    [Fact]
    public async Task TheFrameSlider_StepsThroughTheRun_InWords()
    {
        var lab = await NewLab();
        await lab.ChooseStepsAsync(5);
        Assert.Equal("Step 5 of 5: the finished cloud, after taking noise out 5 times.", lab.FrameText);
        lab.Frame = 0;
        Assert.Equal(Points(lab.Frames[0]), lab.CurrentPoints);
        Assert.Equal($"Step 0 of 5: {FewPoints} points of pure random noise. No noise has been taken out yet.", lab.FrameText);
        lab.Frame = 1;
        Assert.Equal("Step 1 of 5: the network has taken noise out once; 4 steps to go.", lab.FrameText);
        lab.Frame = 3;
        Assert.Equal(Points(lab.Frames[3]), lab.CurrentPoints);
        Assert.Equal("Step 3 of 5: the network has taken noise out 3 times; 2 steps to go.", lab.FrameText);
        lab.Frame = 99;   // clamped
        Assert.Equal(5, lab.Frame);
        lab.Frame = -1;
        Assert.Equal(0, lab.Frame);
        Assert.Contains("20 steps", DiffusionStepsLabViewModel.JumpTextFor(5));   // each of 5 steps jumps 20 of the 100
    }

    [Fact]
    public async Task ASeedChange_RedoesForwardAndPrediction_AndWorksOutTheRunAgain()
    {
        var lab = await NewLab();
        var (net, schedule, _, _) = await Shipped();
        await lab.ChooseStepsAsync(10);
        lab.ForwardStep = 30;
        lab.Seed = 6;
        await lab.WorkCompleted;
        Assert.Equal(Points(DiffusionSampler.AddNoise(Spiral, 30, schedule, new SeededRandom(6)).Noisy), lab.ForwardPoints);
        Assert.Equal(10, lab.SampleSteps);
        AssertSameFrames(DiffusionSampler.Sample(net, schedule.Respaced(10), FewPoints, new SeededRandom(6)), lab.Frames);
        // The runs kept for seed 1 belong to seed 1: only this seed's run is listed.
        Assert.Contains("10 steps: ", lab.MeasuredText);
        Assert.DoesNotContain("100 steps: ", lab.MeasuredText);
        Assert.Contains("seed 6", lab.MeasuredText);
    }

    [Fact]
    public async Task ASeedChange_WhileARunIsWorkedOut_RestartsItWithTheNewSeed()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        using var reached = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        int calls = 0;
        var lab = new DiffusionStepsLabViewModel(new FamilyModels(TestEnvironment.Content), FewPoints)
        {
            // The first run (the one made while the lab loads) waits until the seed has been changed.
            BeforeSampling = _ => { if (Interlocked.Increment(ref calls) == 1) { reached.Set(); release.Wait(); } },
        };
        await Task.Run(() => reached.Wait());
        lab.Seed = 3;
        release.Set();
        await lab.WorkCompleted;
        Assert.True(lab.IsReady);
        var (net, schedule, _, _) = await Shipped();
        AssertSameFrames(DiffusionSampler.Sample(net, schedule, FewPoints, new SeededRandom(3)), lab.Frames);
        Assert.Contains("seed 3", lab.MeasuredText);
        Assert.DoesNotContain("seed 1", lab.MeasuredText);
    }

    [Fact]
    public async Task AQuickSecondChoice_Wins()
    {
        var lab = await NewLab();
        var first = lab.ChooseStepsAsync(20);
        var second = lab.ChooseStepsAsync(10);
        await Task.WhenAll(first, second);
        await lab.WorkCompleted;
        Assert.Equal(10, lab.SampleSteps);
        Assert.Equal(11, lab.Frames.Count);
    }

    [Fact]
    public async Task Reset_BringsBackSeed1_Step20_And100Steps()
    {
        var lab = await NewLab();
        var hundred = lab.Frames;
        lab.ForwardStep = 70;
        await lab.ChooseStepsAsync(5);
        lab.Reset();
        await lab.WorkCompleted;
        Assert.Equal(1, lab.Seed);
        Assert.Equal(DiffusionStepsLabViewModel.DefaultForwardStep, lab.ForwardStep);
        Assert.Equal(100, lab.SampleSteps);
        Assert.Same(hundred, lab.Frames);   // seed 1's run was kept
        Assert.Equal(100, lab.Frame);

        lab.Seed = 2;
        await lab.WorkCompleted;
        lab.Reset();
        await lab.WorkCompleted;
        AssertSameFrames(hundred, lab.Frames);
        Assert.Contains("seed 1", lab.MeasuredText);
    }

    // ---- screens ----

    [Fact]
    public async Task Screens_AreBaselines_ForwardStepLightsForwardAndPrediction_TheFrameSliderLightsNothing()
    {
        var lab = await NewLab();
        Assert.Equal(new[] { "forward", "prediction", "reverse" }.Order(), lab.ReportedScreens.Order());
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Frame = 40;   // a different frame of the same run
        Assert.Empty(raised);

        lab.ForwardStep = 55;
        Assert.Equal(new[] { "forward", "prediction" }.Order(), raised.Order());
        raised.Clear();

        await lab.ChooseStepsAsync(10);
        Assert.Equal(new[] { "reverse" }, raised);
        raised.Clear();
        await lab.ChooseStepsAsync(100);   // kept: shown at once, and it is a different result
        Assert.Equal(new[] { "reverse" }, raised);
    }

    [Fact]
    public async Task ABadContentFolder_GivesAPlainMessage()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var folder = Path.Combine(Path.GetTempPath(), "aianatomy-diffusion-steps-" + Guid.NewGuid().ToString("N"));
        var lab = new DiffusionStepsLabViewModel(new FamilyModels(folder));
        await lab.WorkCompleted;
        Assert.False(lab.IsReady);
        Assert.Contains("missing", lab.LoadMessage);
        Assert.Empty(lab.Frames);
        lab.ForwardStep = 30;
        lab.Seed = 2;
        lab.Reset();
        await lab.WorkCompleted;
        lab.Deactivate();
    }
}
