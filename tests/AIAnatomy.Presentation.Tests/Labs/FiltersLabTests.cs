using AIAnatomy.Core;
using AIAnatomy.Models.Vision;
using AIAnatomy.Presentation.Labs;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests.Labs;

/// <summary>Lesson 4.6's lab: one 3 by 3 filter sliding over an 8 by 8 digit.</summary>
public sealed class FiltersLabTests
{
    private static readonly double[] VerticalEdge = [-1, 0, 1, -1, 0, 1, -1, 0, 1];
    private static readonly double[] HorizontalEdge = [-1, -1, -1, 0, 0, 0, 1, 1, 1];
    private static readonly double[] Sharpen = [0, -1, 0, -1, 5, -1, 0, -1, 0];

    /// <summary>A lab with its network and test set loaded. With no SynchronizationContext every update is applied where it
    /// is made, so nothing needs pumping.</summary>
    private static async Task<FiltersLabViewModel> NewLab()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var lab = new FiltersLabViewModel(new FamilyModels(TestEnvironment.Content));
        await lab.WorkCompleted;
        Assert.True(lab.IsReady, lab.LoadMessage);
        return lab;
    }

    private static double[] Flat(double[,] map)
    {
        var flat = new double[map.Length];
        int cols = map.GetLength(1);
        for (int r = 0; r < map.GetLength(0); r++)
            for (int c = 0; c < cols; c++) flat[r * cols + c] = map[r, c];
        return flat;
    }

    private static double[] Conv1Row(DigitNet net, int filter) =>
        net.Parameters().Single(p => p.Name == DigitNet.Conv1Name).Value.Data.Skip(filter * 9).Take(9).ToArray();

    // ---- opening ----

    [Fact]
    public async Task Opens_OnSample0_WithTheVerticalEdgePreset()
    {
        var lab = await NewLab();
        Assert.Equal("FiltersLab", FiltersLabViewModel.Id);
        Assert.Equal(FiltersLabViewModel.Id, lab.LabId);
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();

        Assert.Equal(0, lab.SampleIndex);
        Assert.Equal(1740, lab.SampleCount);
        Assert.Equal(set.Label(0), lab.SampleLabel);
        Assert.False(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.Equal(set.Pixels(0), Flat(lab.Image!));
        Assert.Equal(8, lab.Image!.GetLength(0));
        Assert.Equal(8, lab.Image.GetLength(1));

        Assert.Equal(FiltersLabViewModel.VerticalEdgeName, lab.FilterName);
        Assert.Equal(VerticalEdge, lab.Weights);
        Assert.Equal(9, lab.Filter.Count);
        Assert.Equal("The same 9 numbers were used at all 64 positions.", FiltersLabViewModel.CountText);
    }

    [Fact]
    public void HandMadePresets_HaveTheirNumbers()
    {
        var presets = FiltersLabViewModel.HandMadePresets;
        Assert.Equal(new[] { "Vertical edge", "Horizontal edge", "Blur", "Sharpen" }, presets.Select(p => p.Name));
        Assert.Equal(VerticalEdge, presets[0].Weights);
        Assert.Equal(HorizontalEdge, presets[1].Weights);
        Assert.All(presets[2].Weights, w => Assert.Equal(1.0 / 9, w));
        Assert.Equal(Sharpen, presets[3].Weights);
        Assert.All(presets, p => Assert.False(p.IsLearned));
        // The horizontal edge is the vertical edge turned on its side (its transpose).
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++) Assert.Equal(presets[0].Weights[r * 3 + c], presets[1].Weights[c * 3 + r]);
    }

    // ---- the hand-worked case ----

    [Fact]
    public async Task VerticalEdge_OnAHalfDarkHalfLightImage_GivesThreeAlongTheBorder()
    {
        // 0 on the left half (columns 0 to 3) and 1 on the right half (columns 4 to 7). The filter adds the right-hand
        // column of its window and subtracts the left-hand column: at column 3 or 4 that is 1 - 0 in each of 3 rows, so 3;
        // inside either half it is 0. Outside the image counts as 0 (padding 1), so the top and bottom rows have only 2 rows
        // of the window on the image (2 at the border), and at column 7 the window's right column is off the image, so the
        // light half meets the padding: 0 - 1 in each row, -3 (-2 in the corners).
        var lab = await NewLab();
        var image = new double[64];
        for (int r = 0; r < 8; r++)
            for (int c = 4; c < 8; c++) image[r * 8 + c] = 1;
        lab.UseImage(image, "half and half");

        double[] edge = [0, 0, 0, 2, 2, 0, 0, -2];
        double[] middle = [0, 0, 0, 3, 3, 0, 0, -3];
        var expected = new double[64];
        for (int r = 0; r < 8; r++)
            (r is 0 or 7 ? edge : middle).CopyTo(expected, r * 8);
        Assert.Equal(expected, Flat(lab.FeatureMap!));
        Assert.Equal(3, lab.MapLimit);
    }

    [Fact]
    public async Task FeatureMap_IsTheNetworksOwnConvolution_WithBias0()
    {
        var lab = await NewLab();
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        lab.Next();
        lab.ChoosePreset(FiltersLabViewModel.HandMadePresets[3]);
        var input = Tensor.FromArray(set.Pixels(1), 1, 1, 8, 8);
        var expected = Ops.Conv2d(input, Tensor.FromArray(Sharpen, 1, 9), Tensor.Zeros(1), 3, 1);
        Assert.Equal(expected.Shape, new[] { 1, 1, 8, 8 });
        Assert.Equal(expected.Data, Flat(lab.FeatureMap!));
    }

    // ---- learned filters ----

    [Fact]
    public async Task LearnedFilters_AreTheShippedConv1Weights_Exactly()
    {
        var lab = await NewLab();
        var (net, _) = await new FamilyModels(TestEnvironment.Content).DigitsAsync();
        Assert.Equal(16, net.Config.Filters1);
        Assert.Equal(16, lab.LearnedPresets.Count);
        for (int f = 0; f < 16; f++)
        {
            var preset = lab.LearnedPresets[f];
            Assert.Equal($"Learned {f + 1}", preset.Name);
            Assert.True(preset.IsLearned);
            Assert.Equal(Conv1Row(net, f), preset.Weights);

            lab.ChoosePreset(preset);
            Assert.Equal(Conv1Row(net, f), lab.Weights);   // no clamping or rounding on the way in
            Assert.Equal($"Learned {f + 1}", lab.FilterName);
        }
        // The learned filters fit inside the editing range, so choosing one never changes a number.
        Assert.All(lab.LearnedPresets.SelectMany(p => p.Weights), w => Assert.InRange(w, -10, 10));
    }

    [Fact]
    public async Task Learned14_IsARoughHorizontalEdgeTurnedUpsideDown()
    {
        // Lesson 4.6's Try this: Learned 14's top row is all above zero and two of its bottom row are below.
        var lab = await NewLab();
        var w = lab.LearnedPresets[13].Weights;
        Assert.Equal("Learned 14", lab.LearnedPresets[13].Name);
        Assert.All(w.Take(3), v => Assert.True(v > 0, $"top row {v}"));
        Assert.Equal(2, w.Skip(6).Count(v => v < 0));
    }

    // ---- editing ----

    [Fact]
    public async Task Edits_OutsideMinus10To10_AreClamped()
    {
        var lab = await NewLab();
        lab.Filter[0].Value = 15;
        Assert.Equal(10, lab.Filter[0].Value);
        lab.Filter[8].Value = -12.5;
        Assert.Equal(-10, lab.Filter[8].Value);
        lab.Filter[4].Text = "25";
        Assert.Equal(10, lab.Filter[4].Value);
        Assert.Equal("10", lab.Filter[4].Text);
        lab.Filter[4].Text = "−2.5";   // the typographic minus, as the app writes numbers
        Assert.Equal(-2.5, lab.Filter[4].Value);
        lab.Filter[4].Text = "abc";   // not a number: the number stays as it was
        Assert.Equal(-2.5, lab.Filter[4].Value);
        lab.Filter[4].Value = double.NaN;
        Assert.Equal(-2.5, lab.Filter[4].Value);
        Assert.Equal([10, 0, 1, -1, -2.5, 1, -1, 0, -10], lab.Weights);
        Assert.Equal(FiltersLabViewModel.OwnFilterName, lab.FilterName);
    }

    [Fact]
    public async Task AnEdit_RecomputesTheFeatureMap()
    {
        var lab = await NewLab();
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        foreach (var cell in lab.Filter) cell.Value = 0;
        lab.Filter[4].Value = 2;   // the centre only: the map is twice the image
        Assert.Equal(set.Pixels(0).Select(p => 2 * p), Flat(lab.FeatureMap!));
    }

    // ---- choosing the input ----

    [Fact]
    public async Task NextAndPrevious_StepThroughTheTestSet_AndWrapRound()
    {
        var lab = await NewLab();
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        lab.Previous();
        Assert.Equal(1739, lab.SampleIndex);
        Assert.Equal(set.Pixels(1739), Flat(lab.Image!));
        lab.Next();
        lab.Next();
        Assert.Equal(1, lab.SampleIndex);
        Assert.Equal(set.Label(1), lab.SampleLabel);
    }

    [Fact]
    public async Task ShowDigit_PicksTheNextSampleOfThatDigit()
    {
        var lab = await NewLab();
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        lab.ShowDigit(7);
        int first = lab.SampleIndex;
        Assert.Equal(7, set.Label(first));
        Assert.Equal(Enumerable.Range(0, set.Count).First(i => set.Label(i) == 7), first);
        lab.ShowDigit(7);
        Assert.Equal(7, lab.SampleLabel);
        Assert.Equal(Enumerable.Range(first + 1, set.Count - first - 1).First(i => set.Label(i) == 7), lab.SampleIndex);
        Assert.Throws<ArgumentOutOfRangeException>(() => lab.ShowDigit(10));
    }

    [Fact]
    public async Task Drawing_ReplacesTheSample_AndAnEmptyDrawingGivesTheMessage()
    {
        var lab = await NewLab();
        IReadOnlyList<IReadOnlyList<(double X, double Y)>> one = [[(0.5, 0.1), (0.5, 0.9)]];
        lab.UseDrawing(one);
        Assert.True(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.Equal(DigitImage.FromStrokes(one), Flat(lab.Image!));

        lab.UseDrawing([]);
        Assert.Equal(FiltersLabViewModel.EmptyDrawingMessage, lab.Message);
        Assert.False(lab.UsingDrawing);
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        Assert.Equal(set.Pixels(0), Flat(lab.Image!));   // back to the sample it was showing

        lab.UseDrawing(one);
        lab.Next();   // a sample replaces the drawing
        Assert.False(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.Equal(set.Pixels(1), Flat(lab.Image!));
    }

    // ---- screens ----

    [Fact]
    public async Task Screens_AreBaselines_AndEachLightsOncePerChange()
    {
        var lab = await NewLab();
        Assert.Equal(new[] { "image", "feature-map" }.Order(), lab.ReportedScreens.Order());
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => raised.Add(e.Screen);

        lab.Next();   // a new image: both
        Assert.Equal(new[] { "feature-map", "image" }, raised.Order());
        raised.Clear();

        lab.ChoosePreset(FiltersLabViewModel.HandMadePresets[2]);   // nine numbers, one change
        Assert.Equal(["feature-map"], raised);
        raised.Clear();
        lab.ChoosePreset(FiltersLabViewModel.HandMadePresets[2]);   // the same again: nothing
        Assert.Empty(raised);

        lab.Filter[0].Value = 3;
        Assert.Equal(["feature-map"], raised);
        raised.Clear();
        lab.Filter[0].Value = 3;
        Assert.Empty(raised);

        lab.UseDrawing([[(0.5, 0.1), (0.5, 0.9)]]);
        Assert.Equal(new[] { "feature-map", "image" }, raised.Order());
    }

    // ---- reset ----

    [Fact]
    public async Task Reset_RestoresSample0AndTheVerticalEdge()
    {
        var lab = await NewLab();
        var set = await new FamilyModels(TestEnvironment.Content).DigitTestSetAsync();
        lab.ShowDigit(5);
        lab.ChoosePreset(lab.LearnedPresets[3]);
        lab.Filter[2].Value = 7;
        lab.UseDrawing([[(0.2, 0.2)]]);
        lab.UseDrawing([]);
        Assert.NotNull(lab.Message);

        lab.Reset();
        Assert.Equal(0, lab.SampleIndex);
        Assert.False(lab.UsingDrawing);
        Assert.Null(lab.Message);
        Assert.Equal(set.Pixels(0), Flat(lab.Image!));
        Assert.Equal(FiltersLabViewModel.VerticalEdgeName, lab.FilterName);
        Assert.Equal(VerticalEdge, lab.Weights);
    }

    [Fact]
    public async Task ABadContentFolder_GivesAPlainMessage()
    {
        SynchronizationContext.SetSynchronizationContext(null);
        var folder = Path.Combine(Path.GetTempPath(), "aianatomy-filters-" + Guid.NewGuid().ToString("N"));
        var lab = new FiltersLabViewModel(new FamilyModels(folder));
        await lab.WorkCompleted;
        Assert.False(lab.IsReady);
        Assert.Contains("missing", lab.LoadMessage);
        lab.Next();   // nothing to show: no throw
        lab.Reset();
    }
}
