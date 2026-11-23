using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class EmbeddingsTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void PlotHasOnePointPerToken()
    {
        var models = _env.NewActiveModels();
        var lab = new EmbeddingsLabViewModel(models);
        int vocabulary = models.Current.Tokenizer.VocabularySize;

        Assert.Equal(vocabulary, lab.TrainedPoints.Count);
        Assert.Equal(vocabulary, lab.UntrainedPoints.Count);
        Assert.Equal("�", lab.TrainedPoints[0].Label);
        Assert.Equal(EmbeddingsLabViewModel.UnknownGroup, lab.TrainedPoints[0].Group);
        Assert.Contains(lab.TrainedPoints, p => p.Label == "␣" && p.Group == EmbeddingsLabViewModel.SpaceGroup);
        Assert.Contains(lab.TrainedPoints, p => p.Label == "a" && p.Group == EmbeddingsLabViewModel.LettersGroup);
        Assert.Contains("flat picture keeps", lab.TrainedSpreadText);
        Assert.EndsWith("% of the spread.", lab.UntrainedSpreadText);
    }

    [Fact]
    public void SelectingPoint_ShowsItsVector()
    {
        var models = _env.NewActiveModels();
        var lab = new EmbeddingsLabViewModel(models);
        Assert.Null(lab.TrainedVector);

        lab.SelectedIndex = 5;

        Assert.Equal(models.Current.Transformer.Config.ModelWidth, lab.TrainedVector!.Length);
        Assert.Equal(lab.TrainedVector.Length, lab.UntrainedVector!.Length);
        Assert.Equal(lab.TrainedPoints[5].Label, lab.SelectedLabel);
        Assert.NotEqual(lab.TrainedVector, lab.UntrainedVector);
    }

    [Fact]
    public void UntrainedModel_IsTheSameEveryTime_AndNotTheTrainedOne()
    {
        var models = _env.NewActiveModels();
        var a = new EmbeddingsLabViewModel(models);
        var b = new EmbeddingsLabViewModel(models);
        Assert.Equal(a.UntrainedPoints.Select(p => (p.X, p.Y)), b.UntrainedPoints.Select(p => (p.X, p.Y)));
        Assert.NotEqual(a.UntrainedPoints.Select(p => (p.X, p.Y)), a.TrainedPoints.Select(p => (p.X, p.Y)));
    }

    [Fact]
    public void ChangingTheSeed_RedrawsTheUntrainedPicture_AndResetRestoresIt()
    {
        var lab = new EmbeddingsLabViewModel(_env.NewActiveModels());
        var before = lab.UntrainedPoints.Select(p => (p.X, p.Y)).ToList();
        int original = lab.Seed;
        lab.Seed = original + 7;
        Assert.NotEqual(before, lab.UntrainedPoints.Select(p => (p.X, p.Y)).ToList());
        lab.Reset();
        Assert.Equal(original, lab.Seed);
        Assert.Equal(before, lab.UntrainedPoints.Select(p => (p.X, p.Y)).ToList());
    }

    [Fact]
    public void InductionDemo_HasLetterGroupsAndFollowsModelChange()
    {
        var models = _env.NewActiveModels();
        var lab = new EmbeddingsLabViewModel(models);
        lab.SelectedIndex = 3;
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        Assert.Equal(16, lab.TrainedPoints.Count);
        Assert.Equal(-1, lab.SelectedIndex);
        Assert.Equal(15, lab.TrainedPoints.Count(p => p.Group == EmbeddingsLabViewModel.LettersGroup));
    }

    [Theory]
    [InlineData("a", "letters")]
    [InlineData("7", "digits")]
    [InlineData(",", "punctuation")]
    [InlineData(" ", "space or new line")]
    [InlineData("\n", "space or new line")]
    public void GroupOf_ClassifiesCharacters(string text, string group) =>
        Assert.Equal(group, EmbeddingsLabViewModel.GroupOf(3, text));
}
