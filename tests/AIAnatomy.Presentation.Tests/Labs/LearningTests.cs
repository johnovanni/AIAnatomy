using AIAnatomy.Core;
using AIAnatomy.Models.Language;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Visuals;

namespace AIAnatomy.Presentation.Tests.Labs;

public class LearningTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private LearningLabViewModel NewLab() => new(_env.NewActiveModels());

    // ---- (a) perceptron ----

    [Fact]
    public void Xor_NeverReachesZeroErrors()
    {
        var p = NewLab().Perceptron;
        p.Dataset = "XOR";
        Assert.Equal(100, p.EpochLimit);
        p.RunCommand.Execute(null);
        Assert.Equal(100, p.Epochs.Count);
        Assert.All(p.Epochs, e => Assert.True(e.Errors > 0));
        Assert.Contains("No straight line can separate", PerceptronPanelViewModel.XorNote);
        Assert.Contains("hidden layers", PerceptronPanelViewModel.XorNote);
        Assert.True(p.IsXor);
    }

    [Theory]
    [InlineData("AND")]
    [InlineData("OR")]
    public void AndAndOr_ReachZeroErrors_AndSeparateThePoints(string dataset)
    {
        var p = NewLab().Perceptron;
        p.Dataset = dataset;
        Assert.Equal(20, p.EpochLimit);
        p.RunCommand.Execute(null);
        Assert.Equal(0, p.Epochs[^1].Errors);
        Assert.Contains("Done after", p.Status);
        Assert.Equal("No point is on the wrong side.", p.WrongPoints);
        Assert.Single(p.Lines);
        Assert.Equal(2, p.Lines[0].Points.Count);
    }

    [Fact]
    public void StepOneEpoch_GivesTheSameEpochsAsRun()
    {
        var p = NewLab().Perceptron;
        p.Dataset = "OR";
        p.RunCommand.Execute(null);
        var run = p.Epochs.ToList();
        p.Reset();
        p.Dataset = "OR";
        Assert.Empty(p.Epochs);
        for (int i = 0; i < 50; i++) p.StepOneEpochCommand.Execute(null);   // extra steps after finishing do nothing
        Assert.Equal(run, p.Epochs.ToList());
        Assert.True(p.IsFinished);
    }

    [Fact]
    public void Perceptron_DefaultsAndReset()
    {
        var p = NewLab().Perceptron;
        Assert.Equal(0.1, p.Rate);
        Assert.Equal(20, p.EpochLimit);
        Assert.Equal(4, p.Points.Count);
        Assert.Empty(p.Lines);
        Assert.Equal("No decision line yet: both weights are 0.", p.LineText);
        p.Dataset = "XOR";
        p.Rate = 0.5;
        p.StepOneEpochCommand.Execute(null);
        p.Reset();
        Assert.Equal("AND", p.Dataset);
        Assert.Equal(0.1, p.Rate);
        Assert.Equal(20, p.EpochLimit);
        Assert.Empty(p.Epochs);
        Assert.Empty(p.TableRows);
    }

    [Fact]
    public void DecisionLine_LiesWhereTheSumIsZero()
    {
        var p = NewLab().Perceptron;
        p.StepOneEpochCommand.Execute(null);
        Assert.Empty(p.Lines);   // after one epoch the line x1 + x2 + 1 = 0 is outside the plot
        Assert.Contains("outside the plot", p.LineText);
        p.RunCommand.Execute(null);
        var last = p.Epochs[^1];
        foreach (var (x, y) in p.Lines[0].Points)
            Assert.Equal(0, last.W1 * x + last.W2 * y + last.Bias, 9);
        Assert.Contains("= 0", p.LineText);
    }

    [Fact]
    public void DecisionLine_Cases()
    {
        Assert.Null(PerceptronPanelViewModel.DecisionLine(0, 0, 0.3));
        Assert.Null(PerceptronPanelViewModel.DecisionLine(0, 0, 0));
        var horizontal = PerceptronPanelViewModel.DecisionLine(0, 2, -1)!;   // y = 0.5
        Assert.All(horizontal, p => Assert.Equal(0.5, p.Y, 9));
        var vertical = PerceptronPanelViewModel.DecisionLine(2, 0, -1)!;     // x = 0.5
        Assert.All(vertical, p => Assert.Equal(0.5, p.X, 9));
        Assert.Null(PerceptronPanelViewModel.DecisionLine(0, 1, -5));         // y = 5, outside the view
    }

    [Fact]
    public void LongRuns_ShowOnlyTheLastTwentyEpochs()
    {
        var p = NewLab().Perceptron;
        p.Dataset = "XOR";
        p.RunCommand.Execute(null);
        Assert.Equal(20, p.TableRows.Count);
        Assert.Equal(100, p.TableRows[^1].Epoch);
        Assert.Equal(81, p.TableRows[0].Epoch);
        Assert.Contains("last 20 of 100", p.TableNote);
    }

    [Fact]
    public void PointsUseTwoGroups_SoMarkerShapesCanTellThemApart()
    {
        var p = NewLab().Perceptron;
        Assert.Equal(["Should be 0", "Should be 1"], p.Points.Select(x => x.Group).Distinct().OrderBy(x => x));
        Assert.Equal(3, p.Points.Count(x => x.Group == "Should be 0"));
    }

    // ---- (b) loss surface ----

    [Fact]
    public void HighRate_ShowsDivergenceMessage()
    {
        var loss = NewLab().Loss;
        Assert.Null(loss.Message);
        loss.Rate = 0.12;
        Assert.Equal("Too large: each step overshoots further", loss.Message);
        Assert.True(loss.IsDiverging);
        loss.Rate = 0.04;
        Assert.Null(loss.Message);
    }

    [Theory]
    [InlineData(0.1, false)]
    [InlineData(0.099, false)]
    [InlineData(0.101, true)]
    [InlineData(0.15, true)]
    [InlineData(0.01, false)]
    public void Divergence_StartsJustAboveRate0point1(double rate, bool diverges)
    {
        var loss = NewLab().Loss;
        loss.Rate = rate;
        Assert.Equal(diverges, loss.Message != null);
    }

    [Fact]
    public void Path_StartsAtTheGivenPoint_AndHasStepsPlusOnePoints()
    {
        var loss = NewLab().Loss;
        Assert.Equal(0.04, loss.Rate);
        Assert.Equal(LossPanelViewModel.Steps + 1, loss.Path.Count);
        Assert.Equal(-4, loss.Path[0].X);
        Assert.Equal(1.5, loss.Path[0].Y);
        Assert.Equal(AIAnatomy.Models.Toys.LossSurface.Loss(-4, 1.5), AIAnatomy.Models.Toys.LossSurface.Loss(loss.Path[0].X, loss.Path[0].Y));
        Assert.Equal(21, loss.Grid.GetLength(0));
        Assert.Equal(51, loss.Grid.GetLength(1));
        Assert.Contains("1 − 20 × rate", LossPanelViewModel.ThresholdNote);
        Assert.Contains("below −1", LossPanelViewModel.ThresholdNote);
        Assert.Contains("0.1", LossPanelViewModel.ThresholdNote);
    }

    [Fact]
    public void FactorText_ShowsTheMultiplier()
    {
        var loss = NewLab().Loss;
        loss.Rate = 0.12;
        Assert.Contains("1 − 20 × 0.12 = −1.400", loss.FactorText);
    }

    // ---- (c) expression graph ----

    [Fact]
    public void GraphGradients_MatchKnownValues()
    {
        var g = NewLab().Graph;
        var byName = g.Rows.ToDictionary(r => r.Name.Split(' ')[0]);
        Assert.Equal(7, g.Rows.Count);
        Assert.Equal((2.0, 6.0), (byName["a"].Value, byName["a"].Gradient));
        Assert.Equal((-3.0, -4.0), (byName["b"].Value, byName["b"].Gradient));
        Assert.Equal((10.0, -2.0), (byName["c"].Value, byName["c"].Gradient));
        Assert.Equal((-2.0, 4.0), (byName["f"].Value, byName["f"].Gradient));
        Assert.Equal((-6.0, -2.0), (byName["e"].Value, byName["e"].Gradient));
        Assert.Equal((4.0, -2.0), (byName["d"].Value, byName["d"].Gradient));
        Assert.Equal((-8.0, 1.0), (byName["L"].Value, byName["L"].Gradient));
        Assert.Equal(7, g.Nodes.Count);
        Assert.All(g.Nodes, n => { Assert.Contains("value", n.Value); Assert.Contains("gradient", n.Value); });
        Assert.Contains("chain rule", GraphPanelViewModel.BackpropNote);
        Assert.Contains("how much L changes if this number changes", GraphPanelViewModel.BackpropNote);
    }

    [Fact]
    public void GraphEdits_UpdateEveryNode_AndBadNumbersAreKept()
    {
        var g = NewLab().Graph;
        g.A = 3; g.B = 4; g.C = 1; g.F = 2;     // e = 12, d = 13, L = 26; dL/da = f·b = 8; dL/db = f·a = 6
        var byName = g.Rows.ToDictionary(r => r.Name.Split(' ')[0]);
        Assert.Equal(26, byName["L"].Value);
        Assert.Equal(8, byName["a"].Gradient);
        Assert.Equal(6, byName["b"].Gradient);
        g.A = double.NaN;
        Assert.Equal(0, g.A);
        g.B = 1e9;
        Assert.Equal(GraphPanelViewModel.Limit, g.B);
        g.Reset();
        Assert.Equal((2.0, -3.0, 10.0, -2.0), (g.A, g.B, g.C, g.F));
        Assert.Equal(-8, g.Rows.Single(r => r.Name.StartsWith("L")).Value);
    }

    [Fact]
    public void Working_UsesTheNodeNumbers()
    {
        var g = NewLab().Graph;
        Assert.Contains("∂L/∂a = ∂L/∂e × b = −2 × −3 = 6", g.Working);
        Assert.Equal(7, g.Working.Count);
    }

    // ---- (d) cross-entropy ----

    [Fact]
    public void CrossEntropy_EqualsMinusLogP()
    {
        var models = _env.NewActiveModels();
        var lab = new LearningLabViewModel(models);
        var ce = lab.CrossEntropy;
        var prediction = models.Current.Predict(ce.Text);
        var logits = prediction.Forward.Logits;
        int v = logits.Shape[1];
        foreach (int position in new[] { 0, 3, ce.PositionChoices.Count - 1 })
        {
            ce.PositionIndex = position;
            int target = prediction.UsedTokenIds[position + 1];
            var one = Tensor.FromArray(logits.Row(position), 1, v);
            double expected = Losses.CrossEntropy(one, [target]).Item();
            Assert.Equal(expected, ce.Loss, 9);
            Assert.Equal(-Math.Log(ce.Probability), ce.Loss, 12);
            var p = Sampling.Probabilities(logits.Row(position), new SamplingSettings(1.0));
            Assert.Equal(p[target], ce.Probability, 12);
        }
    }

    [Fact]
    public void CrossEntropy_MeanIsWhatTrainingLowers()
    {
        var models = _env.NewActiveModels();
        var ce = new LearningLabViewModel(models).CrossEntropy;
        var prediction = models.Current.Predict(ce.Text);
        var logits = prediction.Forward.Logits;
        int places = prediction.UsedTokenIds.Count - 1, v = logits.Shape[1];
        var rows = new double[places * v];
        for (int i = 0; i < places; i++) logits.Row(i).CopyTo(rows, i * v);
        double expected = Losses.CrossEntropy(Tensor.FromArray(rows, places, v), prediction.UsedTokenIds.Skip(1).ToArray()).Item();
        Assert.Equal(expected, ce.MeanLoss, 9);
        Assert.Equal(places, ce.Losses!.Length);
        Assert.Equal(ce.Losses.Average(), ce.MeanLoss, 12);
        Assert.Contains("number training tries to lower", ce.MeanText);
    }

    [Fact]
    public void CrossEntropy_ShowsTheActualNextCharacter_AndHighlightsItsBar()
    {
        var ce = NewLab().CrossEntropy;
        ce.Text = "abcabc";
        ce.PositionIndex = 1;          // after "b", the next character is "c"
        Assert.Equal("c", ce.ActualText);
        Assert.Equal("c", ce.HighlightedBar);
        Assert.Contains(ce.Bars, b => b.Label == "c");
        Assert.Equal("2:b", ce.PositionChoices[1]);
        Assert.Equal(NumberText.Percent(ce.Probability), ce.ProbabilityText);
        Assert.Equal(5, ce.PositionChoices.Count);
    }

    [Fact]
    public void CrossEntropy_EmptyShortAndLongText()
    {
        var models = _env.NewActiveModels();
        var ce = new LearningLabViewModel(models).CrossEntropy;
        ce.Text = "";
        Assert.Equal("Type some text to begin", ce.Message);
        Assert.Empty(ce.PositionChoices);
        ce.Text = "a";
        Assert.Equal(CrossEntropyPanelViewModel.NeedTwo, ce.Message);
        int n = models.Current.Transformer.Config.ContextLength;
        ce.Text = new string('a', n + 10);
        Assert.Null(ce.Message);
        Assert.Equal(LabText.TruncationNote(n), ce.Notice);
        Assert.Equal(n - 1, ce.PositionChoices.Count);
    }

    [Fact]
    public void CrossEntropy_FollowsTheActiveModel_AndWorksWithTheLettersDemo()
    {
        var models = _env.NewActiveModels();
        var lab = new LearningLabViewModel(models);
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        var ce = lab.CrossEntropy;
        Assert.Equal(LabText.DefaultLetters, ce.Text);
        Assert.Null(ce.Message);
        var prediction = models.Current.Predict(ce.Text);
        Assert.Equal(prediction.UsedTokenIds.Count - 1, ce.PositionChoices.Count);
        Assert.True(double.IsFinite(ce.MeanLoss));
    }

    [Fact]
    public void CrossEntropy_ContextSentenceNamesThePlaceAndItsCharacter()
    {
        var ce = NewLab().CrossEntropy;
        ce.Text = "abcabc";
        ce.PositionIndex = 1;
        Assert.StartsWith("After place 2 (b), the text so far is: ab", ce.ContextText);
    }

    [Fact]
    public void LearningRate_OnlyScalesTheWeights_SoErrorsAndLineStay()
    {
        var p = NewLab().Perceptron;
        p.Dataset = "OR";
        p.RunCommand.Execute(null);
        var errors = p.Epochs.Select(e => e.Errors).ToList();
        var line = p.Lines[0].Points;
        p.Rate = 0.5;
        p.RunCommand.Execute(null);
        Assert.Equal(errors, p.Epochs.Select(e => e.Errors).ToList());
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(line[i].X, p.Lines[0].Points[i].X, 9);
            Assert.Equal(line[i].Y, p.Lines[0].Points[i].Y, 9);
        }
        Assert.Contains("only scales the weights", PerceptronPanelViewModel.RateNote);
    }

    [Fact]
    public void PerceptronStatus_UsesTheTableEpochCount_EvenAfterTheLimitIsLowered()
    {
        var p = NewLab().Perceptron;
        p.Dataset = "XOR";
        p.RunCommand.Execute(null);
        p.EpochLimit = 10;
        Assert.StartsWith("Epoch 100:", p.Status);
        Assert.Contains("epoch limit is 10", p.Status);
        Assert.Equal(100, p.Epochs[^1].Epoch);
    }

    [Fact]
    public void Captions_FollowTheAccuracyRuling()
    {
        Assert.Contains("the neuron answers 1 on the side of the line where the sum is above 0", PerceptronPanelViewModel.LineCaption);
        Assert.Equal("The start, the first five steps and the last.", LossPanelViewModel.TableNote);
    }

    [Theory]
    [InlineData("0.", 0.0)]
    [InlineData("0.5", 0.5)]
    [InlineData("-0.5", -0.5)]
    [InlineData("1e1", 10.0)]
    [InlineData(" 7 ", 7.0)]
    public void GraphBoxes_TakeDecimalsAsTheyAreTyped(string typed, double expected)
    {
        var g = NewLab().Graph;
        g.AText = typed;
        Assert.Equal(expected, g.A);
        Assert.Equal(typed, g.AText);   // the box is not rewritten while typing
    }

    [Fact]
    public void GraphBoxes_TypingMinusThenDigits_WorksStepByStep()
    {
        var g = NewLab().Graph;
        foreach (string step in new[] { "-", "-0", "-0.", "-0.2" })
        {
            g.BText = step;
            Assert.Equal(step, g.BText);
        }
        Assert.Equal(-0.2, g.B);
        Assert.Equal(0.4, g.Rows.Single(r => r.Name == "a").Gradient, 9);   // dL/da = f * b = -2 * -0.2
    }

    [Fact]
    public void GraphBoxes_TextThatIsNotANumberKeepsTheLastGoodNumber()
    {
        var g = NewLab().Graph;
        g.BText = "-0.2";
        g.BText = "-";
        Assert.Equal(-0.2, g.B);
        g.BText = "";
        Assert.Equal(-0.2, g.B);
        // dL/da = f * b = -2 * -0.2
        Assert.Equal(0.4, g.Rows.Single(r => r.Name == "a").Gradient, 9);
    }

    [Fact]
    public void GraphBoxes_LargeAndNotANumberTextAreCorrected()
    {
        var g = NewLab().Graph;
        g.CText = "5000";
        Assert.Equal(100, g.C);
        Assert.Equal("100", g.CText);
        g.FText = "NaN";
        Assert.Equal(0, g.F);
        Assert.Equal("0", g.FText);
        g.Reset();
        Assert.Equal("2", g.AText);
        Assert.Equal("-3", g.BText);
        Assert.Equal("10", g.CText);
        Assert.Equal("-2", g.FText);
    }

    [Fact]
    public void GraphBoxes_ProgrammaticChange_UpdatesTheBox()
    {
        var g = NewLab().Graph;
        g.A = 0.25;
        Assert.Equal("0.25", g.AText);
        g.A = -4;
        Assert.Equal("-4", g.AText);
    }

    [Fact]
    public void LabReset_RestoresAllFourParts()
    {
        var lab = NewLab();
        lab.Perceptron.Dataset = "OR";
        lab.Loss.Rate = 0.12;
        lab.Graph.A = 9;
        lab.CrossEntropy.Text = "zzz";
        lab.Reset();
        Assert.Equal("AND", lab.Perceptron.Dataset);
        Assert.Equal(0.04, lab.Loss.Rate);
        Assert.Equal(2, lab.Graph.A);
        Assert.Equal(LabText.DefaultText, lab.CrossEntropy.Text);
    }

    // ---- screens (UI rework, Task 15) ----

    private static List<string> Listen(LearningLabViewModel lab)
    {
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        return raised;
    }

    [Fact]
    public void TheNeuronLightsAfterADirectChange_NotWhenTheSameValueIsSetAgain()
    {
        var lab = NewLab();
        var raised = Listen(lab);
        Assert.Empty(raised);   // opening is the baseline
        lab.Perceptron.Dataset = "OR";
        Assert.Equal(["neuron"], raised);
        raised.Clear();
        lab.Perceptron.Dataset = "OR";
        Assert.Empty(raised);
        lab.Perceptron.RunCommand.Execute(null);   // one press of Run: one light-up
        Assert.Equal(["neuron"], raised);
        raised.Clear();
        lab.Perceptron.RunCommand.Execute(null);   // the same epochs again
        Assert.Empty(raised);
    }

    [Fact]
    public void TheSurfaceTheGraphAndTheModelLossLightAfterDirectChanges_NotWhenUnchanged()
    {
        var lab = NewLab();
        var raised = Listen(lab);
        lab.Loss.Rate = 0.1;
        Assert.Equal(["surface"], raised);
        raised.Clear();
        lab.Loss.Rate = 0.1;
        Assert.Empty(raised);

        lab.Graph.AText = "3";
        Assert.Equal(["backprop"], raised);
        raised.Clear();
        lab.Graph.AText = "3";
        Assert.Empty(raised);
        lab.Graph.AText = "-";   // not a number yet: nothing changes
        Assert.Empty(raised);

        lab.CrossEntropy.Text = "The crow sat on the ";
        Assert.Equal(["crossentropy"], raised);
        raised.Clear();
        lab.CrossEntropy.Text = "The crow sat on the ";
        Assert.Empty(raised);
        lab.CrossEntropy.PositionIndex = 0;
        Assert.Equal(["crossentropy"], raised);
    }

    [Fact]
    public void ResetAndModelSwitch_LightTheScreensThatChanged()
    {
        var models = _env.NewActiveModels();
        var lab = new LearningLabViewModel(models);
        var raised = Listen(lab);
        lab.Perceptron.Dataset = "XOR";
        lab.Loss.Rate = 0.1;
        lab.Graph.AText = "5";
        lab.CrossEntropy.Text = "abc";
        raised.Clear();
        lab.Reset();
        Assert.Equal(["backprop", "crossentropy", "neuron", "surface"], raised.Distinct().Order().ToArray());
        Assert.Equal(raised.Count, raised.Distinct().Count());

        raised.Clear();
        lab.Reset();
        Assert.Empty(raised);

        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Equal(["crossentropy"], raised);
    }
}
