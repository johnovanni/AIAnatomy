using AIAnatomy.Models.Inspection;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

public class AttentionTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private AttentionLabViewModel NewLab() => new(_env.NewActiveModels());

    /// <summary>Waits for the training's first loss report (every 10 steps) instead of a fixed delay (final review M18).</summary>
    private static async Task FirstReport(InductionPanelViewModel induction)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (induction.Progress <= 0)
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("The training did not report in time.");
            await Task.Delay(10);
        }
    }

    // ---- the toy ----

    [Fact]
    public void Toy_WeightsSumToOne()
    {
        var toy = new AttentionToyViewModel();
        Assert.Equal(1.0, toy.Weights.Sum(), 12);
        Assert.Equal(AttentionToyViewModel.DefaultKeys, toy.Weights.Length);
        toy.Query.X = -2.5; toy.Query.Y = 3;
        toy.AddKeyCommand.Execute(null);
        Assert.Equal(4, toy.Weights.Length);
        Assert.Equal(1.0, toy.Weights.Sum(), 12);
    }

    [Fact]
    public void Toy_NumbersAgreeWithADirectCalculation()
    {
        var toy = new AttentionToyViewModel();
        double[] dots = toy.AllKeys.Take(3).Select(k => toy.Query.X * k.X + toy.Query.Y * k.Y).ToArray();
        double[] scaled = dots.Select(d => d / Math.Sqrt(2)).ToArray();
        double total = scaled.Sum(Math.Exp);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(dots[i], toy.Rows[i].Dot, 12);
            Assert.Equal(scaled[i], toy.Rows[i].Scaled, 12);
            Assert.Equal(Math.Exp(scaled[i]) / total, toy.Rows[i].Weight, 12);
        }
        double x = Enumerable.Range(0, 3).Sum(i => toy.Rows[i].Weight * toy.AllValues[i].X);
        double y = Enumerable.Range(0, 3).Sum(i => toy.Rows[i].Weight * toy.AllValues[i].Y);
        Assert.Equal(x, toy.AnswerX, 12);
        Assert.Equal(y, toy.AnswerY, 12);
        Assert.Equal(toy.AnswerX, toy.Output.X, 12);
        Assert.Contains("0.707", toy.ScaleText);
    }

    [Fact]
    public void Toy_MovingQueryTowardKey_RaisesItsWeight()
    {
        var toy = new AttentionToyViewModel();
        var key = toy.AllKeys[1];   // K2 points up
        toy.Query.X = 0; toy.Query.Y = 0;
        double previous = toy.Weights[1];
        for (int step = 1; step <= 5; step++)
        {
            toy.Query.X = key.X * step * 0.4;
            toy.Query.Y = key.Y * step * 0.4;
            Assert.True(toy.Weights[1] > previous, $"step {step}");
            previous = toy.Weights[1];
        }
    }

    [Fact]
    public void Toy_KeyCountStaysFromTwoToFour_AndResetRestores()
    {
        var toy = new AttentionToyViewModel();
        toy.RemoveKeyCommand.Execute(null);
        Assert.False(toy.RemoveKeyCommand.CanExecute(null));
        Assert.Equal(2, toy.ActiveKeys.Count);
        toy.KeyCount = 9;
        Assert.Equal(4, toy.KeyCount);
        Assert.Equal(5, toy.KeyPlotHandles.Count);   // query + 4 keys
        Assert.Equal(5, toy.ValuePlotHandles.Count); // 4 values + the answer
        toy.Query.X = 1.7;
        toy.Reset();
        Assert.Equal(3, toy.KeyCount);
        Assert.Equal(1.0, toy.Query.X);
    }

    [Fact]
    public void DragHandle_KeepsNumbersInsideThePlot()
    {
        var h = new DragHandle("Q", 0, 0);
        h.X = 99; h.Y = double.NaN;
        Assert.Equal(3.0, h.X);
        Assert.Equal(0.0, h.Y);
    }

    // ---- the real heat maps ----

    [Fact]
    public void Real_HeatMapRowsSumToOne()
    {
        var lab = NewLab();
        var w = lab.RealWeights!;
        Assert.Equal(lab.TokenLabels.Count, w.GetLength(0));
        for (int r = 0; r < w.GetLength(0); r++)
        {
            double sum = 0;
            for (int c = 0; c <= r; c++) sum += w[r, c];
            Assert.Equal(1.0, sum, 9);
        }
    }

    [Fact]
    public void Real_FutureCellsBlocked()
    {
        var lab = NewLab();
        var w = lab.RealWeights!;
        for (int r = 0; r < w.GetLength(0); r++)
            for (int c = r + 1; c < w.GetLength(1); c++)
                Assert.True(double.IsNaN(w[r, c]), $"({r},{c})");
        Assert.False(double.IsNaN(w[3, 3]));
    }

    [Fact]
    public void Real_HeadPicker_ChangesTheMap_AndClampsOnAModelChange()
    {
        var models = _env.NewActiveModels();
        var lab = new AttentionLabViewModel(models);
        var first = (double[,])lab.RealWeights!.Clone();
        lab.Layer = 2; lab.Head = 3;
        Assert.NotEqual(first[2, 0], lab.RealWeights![2, 0]);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));   // 2 layers, 2 heads
        lab.OnActiveModelChanged();
        Assert.Equal(2, lab.Head);
        Assert.Equal(2, lab.HeadNumbers.Count);
        Assert.Equal(AttentionLabViewModel.DefaultLetters.Length, lab.TokenLabels.Count);   // letters: the default text has unknown characters there
    }

    [Fact]
    public void Real_EmptyLongAndUnknownText()
    {
        var lab = NewLab();
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Null(lab.RealWeights);
        lab.Text = new string('a', 70) + "\U0001F642";
        Assert.Null(lab.Message);
        Assert.Equal("Only the last 64 characters are used: that is this model's context length.", lab.Notice);
        Assert.Equal(64, lab.RealWeights!.GetLength(0));
        Assert.EndsWith("�", lab.TokenLabels[^1]);
    }

    // ---- the induction experiment ----

    [Fact]
    public void Induction_CellsFollowRuleR16()
    {
        var seq = new InductionSequence(new int[17], FirstStart: 2, SecondStart: 9, Length: 7);
        var cells = InductionPanelViewModel.InductionCells(seq);
        Assert.Equal(7, cells.Count);
        Assert.Equal(new GridCell(9, 3), cells[0]);
        Assert.Equal(new GridCell(15, 9), cells[6]);
        var clipped = InductionPanelViewModel.InductionCells(new InductionSequence(new int[17], 3, 11, 6));   // rows 11..16: the last is outside
        Assert.Equal(5, clipped.Count);
        Assert.All(clipped, c => Assert.True(c.Row <= 15));
    }

    [Fact]
    public async Task Induction_DemoShowsHeadAbove0_6()
    {
        var models = _env.NewActiveModels();
        var before = models.Current;
        var lab = new AttentionLabViewModel(models);
        await lab.Induction.ShowDemoCommand.ExecuteAsync(null);

        var induction = lab.Induction;
        Assert.Equal(4, induction.Scores.Count);   // every head of both layers
        Assert.Contains(induction.Scores, s => s.Layer == 2 && s.Induction > 0.6);
        Assert.Contains(induction.Scores, s => s.Layer == 1 && s.PreviousToken > 0);
        Assert.Same(before, models.Current);   // the demo never becomes the active model
        Assert.NotNull(induction.HeatMap);
        Assert.NotEmpty(induction.OutlinedCells);
        Assert.Equal(17, induction.SequenceCells.Count);
        Assert.Equal(induction.SequenceCells.Count(c => c.Copy == "first copy"), induction.SequenceCells.Count(c => c.Copy == "second copy"));
        var best = induction.Scores.OrderByDescending(s => s.Induction).First();
        Assert.Equal((best.Layer, best.Head), (induction.Layer, induction.Head));   // the picked head is the strongest one
        Assert.True(double.IsNaN(induction.HeatMap![0, 1]));                        // a future position is blocked
        Assert.False(double.IsNaN(induction.HeatMap[4, 3]));
    }

    [Fact]
    public async Task Induction_TrainCanBeCancelled()
    {
        var lab = new AttentionLabViewModel(_env.NewActiveModels());
        var induction = lab.Induction;
        induction.Steps = 100_000;   // far more than can finish
        var run = induction.TrainCommand.ExecuteAsync(null);
        Assert.True(induction.IsTraining);
        await FirstReport(induction);
        induction.TrainCancelCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.False(induction.IsTraining);
        Assert.Contains("stopped", induction.Message);
        Assert.True(induction.Progress < 0.5);
        Assert.Equal(4, induction.Scores.Count);
    }

    [Fact]
    public async Task Induction_TrainingReportsLossEveryTenStepsAndScoresAtStep100()
    {
        var lab = new AttentionLabViewModel(_env.NewActiveModels());
        var induction = lab.Induction;
        induction.Steps = 100;
        await induction.TrainCommand.ExecuteAsync(null);

        Assert.False(induction.IsTraining);
        Assert.Null(induction.Message);
        Assert.Equal(1.0, induction.Progress);
        var points = induction.LossSeries.Single().Points;
        Assert.Equal(10, points.Count);   // every 10th step
        Assert.Equal(10, points[0].X);
        Assert.Equal(100, points[^1].X);
        Assert.True(points[^1].Y < points[0].Y);   // it learned something
        Assert.Contains("Step 100 of 100", induction.ProgressText);
        Assert.Equal(4, induction.Scores.Count);
    }

    [Fact]
    public async Task Induction_ShowDemoWhileTraining_IsNotOverwrittenByTheTraining()
    {
        var lab = NewLab();
        var induction = lab.Induction;
        induction.Steps = 100_000;
        var run = induction.TrainCommand.ExecuteAsync(null);
        await FirstReport(induction);
        await induction.ShowDemoCommand.ExecuteAsync(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        await lab.WorkCompleted;

        Assert.False(induction.IsTraining);
        Assert.Contains("trained demo", induction.Source);
        Assert.Null(induction.Message);   // no "Training was stopped" note
        Assert.Contains(induction.Scores, s => s.Layer == 2 && s.Induction > 0.6);   // the demo's scores, not a half-trained model's
    }

    [Fact]
    public async Task Induction_SeedIsRefusedWhileTraining_AndAppliesAfterwards()
    {
        var lab = NewLab();
        var induction = lab.Induction;
        induction.Steps = 100_000;
        var run = induction.TrainCommand.ExecuteAsync(null);
        await FirstReport(induction);
        var cells = induction.SequenceCells.Select(c => c.Letter).ToList();
        lab.Seed = 9;
        Assert.Equal(1, induction.Seed);
        Assert.Equal(1, lab.Seed);   // the lab's box goes back too
        Assert.Equal(cells, induction.SequenceCells.Select(c => c.Letter).ToList());
        induction.TrainCancelCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));

        lab.Seed = 9;
        Assert.Equal(9, induction.Seed);
        await lab.WorkCompleted;
        Assert.NotEqual(cells, induction.SequenceCells.Select(c => c.Letter).ToList());
    }

    [Fact]
    public void Toy_MovingAPoint_KeepsTheSameHandleLists()
    {
        var toy = new AttentionToyViewModel();
        var keys = toy.ActiveKeys;
        var keyPlot = toy.KeyPlotHandles;
        var valuePlot = toy.ValuePlotHandles;
        toy.Query.X = 2; toy.AllKeys[0].Y = -1; toy.AllValues[1].X = 0.5;
        Assert.Same(keys, toy.ActiveKeys);   // a box being typed in is not rebuilt, so it keeps the keyboard focus
        Assert.Same(keyPlot, toy.KeyPlotHandles);
        Assert.Same(valuePlot, toy.ValuePlotHandles);
        toy.AddKeyCommand.Execute(null);
        Assert.NotSame(keys, toy.ActiveKeys);
        Assert.Equal(4, toy.ActiveKeys.Count);
    }

    [Fact]
    public void Induction_WordingDoesNotOverclaim()
    {
        Assert.Contains("similar to the sudden improvement", InductionPanelViewModel.LossNote);
        Assert.Contains("researchers saw when induction heads formed", InductionPanelViewModel.LossNote);
        Assert.DoesNotContain("identical", InductionPanelViewModel.LossNote);
        Assert.Contains("not guaranteed", InductionPanelViewModel.LossNote);
        Assert.Contains("previous-token head in layer 1", InductionPanelViewModel.CircuitNote);
        Assert.Contains("Olsson et al., 2022", InductionPanelViewModel.CircuitNote);
    }

    [Fact]
    public async Task Reset_PutsEverythingBack()
    {
        var lab = NewLab();
        lab.Text = "x"; lab.Layer = 2; lab.Seed = 5;
        lab.Toy.Query.X = 2;
        lab.Reset();
        await lab.WorkCompleted;
        Assert.Equal(AttentionLabViewModel.DefaultText, lab.Text);
        Assert.Equal(1, lab.Layer);
        Assert.Equal(1, lab.Seed);
        Assert.Equal(1, lab.Induction.Seed);
        Assert.Equal(1.0, lab.Toy.Query.X);
        Assert.Equal(4, lab.Induction.Scores.Count);   // back to the demo that the lab opens with
        Assert.False(lab.Induction.IsTraining);
    }

    [Fact]
    public async Task Reset_StopsTrainingAndGoesBackToTheDemo()
    {
        var lab = NewLab();
        lab.Induction.Steps = 100_000;
        var run = lab.Induction.TrainCommand.ExecuteAsync(null);
        await FirstReport(lab.Induction);
        lab.Reset();
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        await lab.WorkCompleted;
        Assert.False(lab.Induction.IsTraining);
        Assert.Contains("trained demo", lab.Induction.Source);
        Assert.Empty(lab.Induction.LossSeries);
    }

    [Fact]
    public async Task Induction_SeedChangedWhileTheDemoLoads_ShowsTheNewSeed()
    {
        // Final review I2: the demo started by the constructor must not show the old seed's sequence when it finishes.
        var lab = NewLab();
        lab.Seed = 5;
        await lab.WorkCompleted;
        var expected = InductionExperiment.MakeSequence(new AIAnatomy.Core.SeededRandom(5));
        Assert.Equal(5, lab.Induction.Seed);
        Assert.Equal(expected.Tokens.Select(t => InductionExperiment.Tokenizer.TokenText(t)).ToList(),
            lab.Induction.SequenceCells.Select(c => c.Letter).ToList());
        Assert.Equal(expected.SecondStart, lab.Induction.CopySpans[1].Column);
    }

    [Fact]
    public async Task Induction_OpensWithTheDemoOnShow()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var induction = lab.Induction;
        Assert.True(induction.HasModel);
        Assert.Contains("trained demo", induction.Source);
    }

    [Fact]
    public async Task OpeningTheLab_LightsNothing()
    {
        var lab = NewLab();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        await lab.WorkCompleted;   // the trained demo arrives: that first fill is the baseline
        Assert.Empty(raised);
    }

    [Fact]
    public async Task ToyAndRealScreensLightAfterDirectChanges_NotWhenUnchanged()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Toy.Query.X = 2.0;
        Assert.Equal(["toy"], raised);
        raised.Clear();
        lab.Toy.Query.X = 2.0;
        Assert.Empty(raised);

        lab.Text = "the fox";
        Assert.Equal(["real-heads"], raised);
        raised.Clear();
        lab.Text = "the fox";
        Assert.Empty(raised);

        lab.Head = 2;
        Assert.Equal(["real-heads"], raised);
    }

    [Fact]
    public async Task InductionScreens_StayDarkDuringARun_AndLightOnceAtItsEnd()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        var induction = lab.Induction;
        induction.Steps = 100_000;
        var run = induction.TrainCommand.ExecuteAsync(null);
        await FirstReport(induction);
        lock (raised) Assert.Empty(raised);   // the live loss and progress are not light-ups
        induction.TrainCancelCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(60));
        await lab.WorkCompleted;

        lock (raised)
        {
            Assert.Equal(1, raised.Count(s => s == "loss"));
            Assert.Equal(1, raised.Count(s => s == "scores"));
            Assert.Equal(1, raised.Count(s => s == "heat"));
            Assert.DoesNotContain("sequence", raised);
            Assert.Equal(raised.Count, raised.Distinct().Count());
            raised.Clear();
        }

        await induction.ShowDemoCommand.ExecuteAsync(null);   // a second run: back to the demo
        await lab.WorkCompleted;
        lock (raised)
        {
            Assert.Equal(raised.Count, raised.Distinct().Count());
            Assert.Contains("loss", raised);
            Assert.Contains("scores", raised);
        }
    }

    [Fact]
    public async Task ChangingTheSeed_LightsTheSequenceAndTheHeadsOnceTogether()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Seed = 9;
        await lab.WorkCompleted;   // the sequence and the heads measured on it light together, when the measuring ends
        lock (raised)
        {
            Assert.True(raised.Count == raised.Distinct().Count(), string.Join(",", raised));
            Assert.Contains("sequence", raised);
            Assert.Contains("heat", raised);
        }
        raised.Clear();
        lab.Seed = 9;
        await lab.WorkCompleted;
        Assert.Empty(raised);
    }

    [Fact]
    public async Task ResetAndModelSwitch_LightTheScreensThatChanged_AndToyResetAtStartIsDark()
    {
        var models = _env.NewActiveModels();
        var lab = new AttentionLabViewModel(models);
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Toy.Reset();   // already at the start: the net change is nothing
        Assert.Empty(raised);

        lab.Toy.Query.X = 2.0;
        raised.Clear();
        lab.Toy.Reset();
        Assert.Equal(["toy"], raised);

        lab.Text = "the fox";
        raised.Clear();
        lab.Reset();
        await lab.WorkCompleted;
        lock (raised) Assert.Contains("real-heads", raised);

        raised.Clear();
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        lock (raised) Assert.Contains("real-heads", raised);
    }

    [Fact]
    public async Task AFaultWhileReportingTheEndOfARun_DoesNotLeaveTheRunOpen()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        lab.Induction.Updated += () => throw new InvalidOperationException("boom");
        try { await lab.Induction.ShowDemoCommand.ExecuteAsync(null); } catch (InvalidOperationException) { }

        lock (raised) raised.Clear();
        lab.Text = "the fox";
        lock (raised) Assert.Contains("real-heads", raised);   // a run left open would hold this back
    }
}
