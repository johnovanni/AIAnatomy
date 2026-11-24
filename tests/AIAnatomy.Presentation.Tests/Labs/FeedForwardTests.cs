using AIAnatomy.Models.Inspection;
using AIAnatomy.Presentation.Labs;

namespace AIAnatomy.Presentation.Tests.Labs;

public class FeedForwardTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    private static readonly string Fables = new string(
        File.ReadAllText(Path.Combine(TestEnvironment.Content, "texts", "aesop-fables.txt")).Take(2500).ToArray());

    private FeedForwardLabViewModel NewLab() => new(_env.NewActiveModels(), () => Fables);

    [Fact]
    public async Task TopContextsSortedDescending()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        Assert.Equal(10, lab.Contexts.Count);
        Assert.Equal(lab.Contexts.OrderByDescending(c => c.Value).Select(c => c.Value), lab.Contexts.Select(c => c.Value));
        Assert.Equal(Enumerable.Range(1, 10), lab.Contexts.Select(c => c.Rank));
        Assert.All(lab.Contexts, c => Assert.False(string.IsNullOrEmpty(c.Own)));
    }

    [Fact]
    public async Task ChangingNeuron_UpdatesContexts()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var first = lab.Contexts.Select(c => c.Value).ToArray();
        lab.Neuron = 7;
        await lab.WorkCompleted;
        var second = lab.Contexts.Select(c => c.Value).ToArray();
        Assert.NotEqual(first, second);
        var direct = NeuronProbe.TopContexts(_env.NewActiveModels().Current, Fables, 0, 6, 10);
        Assert.Equal(direct.Select(d => d.Value), second);
    }

    [Fact]
    public async Task ContextsAreCachedPerLayerAndNeuron()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var contexts = lab.Contexts;
        lab.Neuron = 2;
        await lab.WorkCompleted;
        lab.Neuron = 1;
        Assert.Same(contexts, lab.Contexts);   // straight from the cache, no new search
    }

    [Fact]
    public async Task ActivationStrip_HasOneNumberPerToken()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        Assert.Equal(LabText.DefaultText.Length, lab.Activations!.Length);
        lab.Text = "";
        Assert.Equal("Type some text to begin", lab.Message);
        Assert.Null(lab.Activations);
        lab.Text = new string('a', 70);
        Assert.Equal("Only the last 64 characters are used: that is this model's context length.", lab.Notice);
    }

    [Fact]
    public async Task ContextsShowMarksAndKeepOwnCharacter()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        Assert.All(lab.Contexts, c => Assert.DoesNotContain(' ', c.Before + c.Own));
        Assert.All(lab.Contexts, c => Assert.DoesNotContain('\n', c.Before + c.Own));
    }

    [Fact]
    public async Task ChangingLayer_RunsANewSearch()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var layer1 = lab.Contexts.Select(c => c.Value).ToArray();
        lab.Layer = 2;
        await lab.WorkCompleted;
        Assert.NotEqual(layer1, lab.Contexts.Select(c => c.Value).ToArray());
    }

    [Fact]
    public async Task InductionDemo_ShowsNoteAndDoesNotThrow()
    {
        var models = _env.NewActiveModels();
        var lab = new FeedForwardLabViewModel(models, () => Fables);
        await lab.WorkCompleted;
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));
        lab.OnActiveModelChanged();
        await lab.WorkCompleted;
        Assert.False(lab.HasFeedForward);
        Assert.Equal("This model has no feed-forward layers (it is attention-only).", lab.Message);
        Assert.Empty(lab.Contexts);
        lab.Reset();
        await lab.WorkCompleted;
        Assert.False(lab.HasFeedForward);
    }

    [Fact]
    public async Task Deactivate_CancelsTheSearch()
    {
        var lab = new FeedForwardLabViewModel(_env.NewActiveModels(), () => new string('a', 20000));
        lab.Deactivate();
        await lab.WorkCompleted;
        Assert.Empty(lab.Contexts);
    }

    [Fact]
    public void ResearchNote_IsTheRuledWording()
    {
        Assert.Equal(
            "Research suggests that feed-forward layers act partly like a store of patterns the model has learned (Geva et al., 2021). " +
            "This is an active research area, not settled. A single neuron often does not have one tidy meaning.",
            FeedForwardLabViewModel.ResearchNote);
    }

    [Fact]
    public async Task TokenKey_IsNumberedFromOne_LikeEveryOtherLab()
    {
        // R30 / final review I1: places in a text are numbered from 1 everywhere (the strip under it uses IndexBase="1").
        var lab = NewLab();
        await lab.WorkCompleted;
        Assert.StartsWith("1:t", lab.TokenKey);
        Assert.Contains($"   {lab.Activations!.Length}:", lab.TokenKey);
        Assert.DoesNotContain("0:", lab.TokenKey.Split("   ")[0]);
        Assert.DoesNotContain($"{lab.Activations.Length + 1}:", lab.TokenKey);
    }

    [Fact]
    public async Task OpeningTheLab_LightsNothing()
    {
        var lab = NewLab();
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };
        await lab.WorkCompleted;   // the first search result is the baseline of its screen
        Assert.Empty(raised);
    }

    [Fact]
    public async Task ScreensLightAfterDirectChanges_NotWhenUnchanged()
    {
        var lab = NewLab();
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Text = "The crow sat on the ";
        Assert.Equal(["activations"], raised);
        raised.Clear();
        lab.Text = "The crow sat on the ";
        Assert.Empty(raised);

        lab.Neuron = 2;   // the strip lights at once; the ten places light once when the search ends
        await lab.WorkCompleted;
        lock (raised)
        {
            Assert.Equal(1, raised.Count(s => s == "activations"));
            Assert.Equal(1, raised.Count(s => s == "contexts"));
        }
    }

    [Fact]
    public async Task ResetAndModelSwitch_LightChangedScreens()
    {
        var models = _env.NewActiveModels();
        var lab = new FeedForwardLabViewModel(models, () => Fables);
        await lab.WorkCompleted;
        var raised = new List<string>();
        lab.ScreenChanged += (_, e) => { lock (raised) raised.Add(e.Screen); };

        lab.Text = "x";
        raised.Clear();
        lab.Reset();
        await lab.WorkCompleted;
        lock (raised) Assert.Contains("activations", raised);

        raised.Clear();
        models.SetActive(models.Library.List().Single(e => e.Name == "Induction demo"));   // attention-only
        lab.OnActiveModelChanged();
        await lab.WorkCompleted;
        lock (raised) { Assert.Contains("activations", raised); Assert.Contains("contexts", raised); }
    }
}
