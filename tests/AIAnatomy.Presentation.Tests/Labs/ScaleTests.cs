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
        Assert.Equal("124 million", gpt2.CountText);
        Assert.Contains("117 million", gpt2.Note);
        Assert.Contains("124 million", gpt2.Note);
        Assert.Contains("radford2019", gpt2.Source);
        Assert.Contains("openai-gpt2-release", gpt2.Source);
        Assert.Equal("No bar is drawn.", claude.CountText);
        Assert.Equal("Claude: size not published. No bar is drawn.", claude.Description);
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
        double F(double n) => (Math.Log10(n) - lab.AxisLow) / (lab.AxisHigh - lab.AxisLow);
        Assert.Equal(F(lab.OurCount), bars[0], 12);
        Assert.Equal(F(124e6), bars[1], 12);
        Assert.Equal(F(175e9), bars[2], 12);
        Assert.Equal(0.132, bars[0], 3);
        Assert.Equal(0.512, bars[1], 3);
        Assert.Equal(0.905, bars[2], 3);
        Assert.Contains("only six steps longer", ScaleLabViewModel.ScaleNote);
        Assert.Contains("1.5 million", lab.RatioSentence);
        Assert.Contains("10 times bigger", ScaleLabViewModel.ScaleNote);
        double ratio = 175e9 / lab.OurCount;
        Assert.Contains("GPT-3 has about", lab.RatioSentence);
        Assert.Contains("times as many parameters as this model", lab.RatioSentence);
        Assert.DoesNotContain("Claude", lab.RatioSentence);
        Assert.True(ratio > 1000);
    }
}
