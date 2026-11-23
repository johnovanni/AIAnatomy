using AIAnatomy.Core;
using AIAnatomy.Models.Toys;
using AIAnatomy.Models.Tokenization;

namespace AIAnatomy.Models.Tests;

public class ToysTests
{
    [Fact]
    public void Perceptron_LearnsAnd()
    {
        var p = new Perceptron();
        var epochs = p.Train(Perceptron.And, 0.1, 100);
        Assert.Equal(0, epochs[^1].Errors);
        Assert.True(epochs.Count < 100);
        foreach (var pt in Perceptron.And) Assert.Equal(pt.Label, p.Predict(pt.X1, pt.X2));
    }

    [Fact]
    public void Perceptron_LearnsOr()
    {
        var p = new Perceptron();
        var epochs = p.Train(Perceptron.Or, 0.1, 100);
        Assert.Equal(0, epochs[^1].Errors);
        foreach (var pt in Perceptron.Or) Assert.Equal(pt.Label, p.Predict(pt.X1, pt.X2));
    }

    [Fact]
    public void Perceptron_NeverSolvesXor()
    {
        var epochs = new Perceptron().Train(Perceptron.Xor, 0.1, 100);
        Assert.Equal(100, epochs.Count);
        Assert.All(epochs, e => Assert.True(e.Errors > 0));
    }

    [Fact]
    public void LossSurface_SmallRateConverges()
    {
        var path = LossSurface.Descend(-4, 1.5, 0.04, 100);
        Assert.Equal(101, path.Count);
        Assert.Equal((-4.0, 1.5, LossSurface.Loss(-4, 1.5)), path[0]);
        Assert.True(path[^1].Loss < 1e-3);
    }

    [Fact]
    public void LossSurface_RateAbove0_1Diverges()
    {
        var path = LossSurface.Descend(-4, 1.5, 0.11, 100);
        Assert.True(path[^1].Loss > path[0].Loss);
    }

    [Fact]
    public void ExpressionGraph_KnownGradients()
    {
        var g = new ExpressionGraphDemo(2, -3, 10, -2);
        g.Run();
        Assert.Equal(new[] { "a", "b", "c", "f", "e = a·b", "d = e + c", "L = d·f" }, g.Nodes.Select(n => n.Label));
        double[] grads = g.Nodes.Select(n => n.Grad![0]).ToArray();
        Assert.Equal(-8, g.Nodes[^1].Data[0]);
        Assert.Equal(6, grads[0]);
        Assert.Equal(-4, grads[1]);
        Assert.Equal(-2, grads[2]);
        Assert.Equal(4, grads[3]);
        Assert.Equal(-2, grads[4]);
        Assert.Equal(-2, grads[5]);
        Assert.Equal(1, grads[6]);
    }

    [Fact]
    public void ExpressionGraph_RunTwiceGivesSameGradients()
    {
        var g = new ExpressionGraphDemo(2, -3, 10, -2);
        g.Run();
        g.Run();
        Assert.Equal(6, g.Nodes[0].Grad![0]);
        Assert.Equal(4, g.Nodes[3].Grad![0]);
    }

    [Fact]
    public void RuleWriter_IsDeterministic()
    {
        var a = new RuleBasedWriter().Write("The fox", 80);
        var b = new RuleBasedWriter().Write("The fox", 80);
        Assert.Equal(80, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void RuleWriter_HasReadableRulesAndAFallback()
    {
        var w = new RuleBasedWriter();
        Assert.InRange(w.Rules.Count, 8, 12);
        Assert.All(w.Rules, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
        Assert.NotNull(w.Rules[^1].Apply(""));
        Assert.NotNull(w.Rules[^1].Apply("zzz"));
        Assert.Equal("u", w.Write("q", 1));
    }

    [Fact]
    public void Bigram_LossFallsWithTraining()
    {
        var tokenizer = CharTokenizer.FromText(TestTexts.Fable);
        var corpus = tokenizer.Encode(TestTexts.Fable);
        var model = new BigramModel(tokenizer, 1);
        double first = model.TrainSteps(corpus, 1, 1.0);
        double last = model.TrainSteps(corpus, 199, 1.0);
        Assert.True(last < first - 0.5, $"first {first}, last {last}");
    }

    [Fact]
    public void Bigram_StartsUniform_AndGeneratesFromLearnedRows()
    {
        var tokenizer = CharTokenizer.FromText(TestTexts.Fable);
        var model = new BigramModel(tokenizer, 1);
        Assert.All(model.Logits.Data, x => Assert.Equal(0, x));
        model.TrainSteps(tokenizer.Encode(TestTexts.Fable), 300, 1.0);
        var text = model.Generate("q", 5, new SeededRandom(3));
        Assert.Equal(5, text.Length);
        Assert.Equal(text, model.Generate("q", 5, new SeededRandom(3)));
    }
}
