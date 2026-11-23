using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class ScaleTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void IncludesUnpublishedClaudeRowWithoutValue()
    {
        var lab = new ScaleLabViewModel(_env.NewActiveModels());
        var claude = lab.Models.Single(r => r.Label.StartsWith("Claude"));
        Assert.Equal("Claude: size not published", claude.Label);
        Assert.Null(claude.Count);
        Assert.Null(claude.BarFraction);
        Assert.Equal(4, lab.Models.Count);
        Assert.Equal(175_000_000_000L, lab.Models.Single(r => r.Label.StartsWith("GPT-3")).Count);
        Assert.Contains("brown2020", lab.Models.Single(r => r.Label.StartsWith("GPT-3")).Source);
        var gpt2 = lab.Models.Single(r => r.Label.StartsWith("GPT-2"));
        Assert.Contains("117 million", gpt2.CountText);
        Assert.Contains("124 million", gpt2.CountText);
        Assert.Contains("radford2019", gpt2.Source);
    }

    [Fact]
    public void OurCountMatchesConfig()
    {
        var models = _env.NewActiveModels();
        var lab = new ScaleLabViewModel(models);
        Assert.Equal(models.Current.Transformer.Config.CountParameters(), lab.OurCount);
        Assert.Equal(lab.OurCount, lab.Parts.Sum(p => p.Count));
        Assert.Equal(ScaleLabViewModel.PartOrder, lab.Parts.Select(p => p.Part).ToArray());
        Assert.Equal(lab.OurCount, lab.Models[0].Count);
    }

    [Fact]
    public void BarsAreLogarithmicAndSentenceUsesRealCounts()
    {
        var lab = new ScaleLabViewModel(_env.NewActiveModels());
        var bars = lab.Models.Where(r => r.BarFraction != null).Select(r => r.BarFraction!.Value).ToArray();
        Assert.Equal(3, bars.Length);
        Assert.True(bars[0] > 0 && bars[0] < bars[1] && bars[1] < bars[2] && bars[2] <= 1);
        Assert.Contains("10 times bigger", ScaleLabViewModel.ScaleNote);
        double ratio = 175e9 / lab.OurCount;
        Assert.Contains("GPT-3 has about", lab.RatioSentence);
        Assert.Contains("times as many parameters as this model", lab.RatioSentence);
        Assert.DoesNotContain("Claude", lab.RatioSentence);
        Assert.True(ratio > 1000);
    }
}
