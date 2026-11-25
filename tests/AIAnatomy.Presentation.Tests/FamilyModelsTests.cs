using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public sealed class FamilyModelsTests : IDisposable
{
    private readonly string _content;

    public FamilyModelsTests()
    {
        // A private copy of the shipped family networks, so a test can delete a file without touching the others.
        _content = Path.Combine(Path.GetTempPath(), "aianatomy-family-" + Guid.NewGuid().ToString("N"));
        var family = Path.Combine(_content, "models", "family");
        Directory.CreateDirectory(family);
        foreach (var file in Directory.GetFiles(Path.Combine(TestEnvironment.Content, "models", "family"), "*.aimodel"))
            File.Copy(file, Path.Combine(family, Path.GetFileName(file)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_content, recursive: true); }
        catch (IOException) { /* temp folder */ }
    }

    private string FamilyFile(string name) => Path.Combine(_content, "models", "family", name);

    [Fact]
    public async Task MemoryAsync_returns_a_working_network()
    {
        var models = new FamilyModels(_content);
        var (rnn, tokenizer, info) = await models.MemoryAsync();
        Assert.Equal(tokenizer.VocabularySize, rnn.Config.VocabularySize);
        Assert.True(info.IsShipped);
        var forward = rnn.Forward(new[] { tokenizer.Encode("the fox") });
        Assert.Equal(new[] { 7, rnn.Config.VocabularySize }, forward.Logits.Shape);
        Assert.All(forward.Logits.Data, v => Assert.True(double.IsFinite(v)));
    }

    [Theory]
    [InlineData("Spiral")]
    [InlineData("Circle")]
    [InlineData("Two moons")]
    [InlineData("Letter S")]
    public async Task DiffusionAsync_returns_a_working_network_for_each_preset(string preset)
    {
        var models = new FamilyModels(_content);
        var (net, schedule, shapeName, info) = await models.DiffusionAsync(preset);
        Assert.Equal(preset, shapeName);
        Assert.True(info.IsShipped);
        var points = Tensor.FromArray(new[] { 0.5, -0.2, 1.0, 1.0 }, new[] { 2, 2 });
        var t = schedule.Timesteps[schedule.Steps / 2];
        var noise = net.Forward(points, new[] { t, t }, schedule.OriginalSteps);
        Assert.Equal(new[] { 2, 2 }, noise.Shape);
        Assert.All(noise.Data, v => Assert.True(double.IsFinite(v)));
    }

    [Fact]
    public async Task Every_shipped_preset_has_a_network()
    {
        var models = new FamilyModels(_content);
        foreach (var (name, _) in Shapes.Presets)
            Assert.Equal(name, (await models.DiffusionAsync(name)).ShapeName);
    }

    [Fact]
    public async Task DiffusionAsync_refuses_an_unknown_preset()
    {
        var models = new FamilyModels(_content);
        await Assert.ThrowsAsync<ArgumentException>(() => models.DiffusionAsync("Triangle"));
    }

    [Fact]
    public async Task Two_memory_calls_give_independent_networks()
    {
        var models = new FamilyModels(_content);
        var first = (await models.MemoryAsync()).Rnn;
        var second = (await models.MemoryAsync()).Rnn;
        Assert.NotSame(first, second);
        double before = second.RecurrentWeights.Value.Data[0];
        first.RecurrentWeights.Value.Data[0] += 1.0;
        Assert.Equal(before, second.RecurrentWeights.Value.Data[0]);
        Assert.NotEqual(first.RecurrentWeights.Value.Data[0], second.RecurrentWeights.Value.Data[0]);
    }

    [Fact]
    public async Task Two_diffusion_calls_give_independent_networks()
    {
        var models = new FamilyModels(_content);
        var first = (await models.DiffusionAsync("Spiral")).Net;
        var second = (await models.DiffusionAsync("Spiral")).Net;
        Assert.NotSame(first, second);
        var firstWeights = first.Parameters().First().Value.Data;
        var secondWeights = second.Parameters().First().Value.Data;
        double before = secondWeights[0];
        firstWeights[0] += 1.0;
        Assert.Equal(before, secondWeights[0]);
    }

    [Fact]
    public async Task A_missing_memory_file_gives_a_plain_message()
    {
        File.Delete(FamilyFile("memory.aimodel"));
        var models = new FamilyModels(_content);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.MemoryAsync());
        Assert.Contains("memory.aimodel", error.Message);
        Assert.DoesNotContain(_content, error.Message);
    }

    [Fact]
    public async Task A_missing_diffusion_file_gives_a_plain_message()
    {
        File.Delete(FamilyFile("diffusion-two-moons.aimodel"));
        var models = new FamilyModels(_content);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.DiffusionAsync("Two moons"));
        Assert.Contains("diffusion-two-moons.aimodel", error.Message);
        // The other presets still load.
        Assert.Equal("Circle", (await models.DiffusionAsync("Circle")).ShapeName);
    }

    [Fact]
    public async Task A_failed_read_is_tried_again_on_the_next_call()
    {
        var saved = File.ReadAllBytes(FamilyFile("memory.aimodel"));
        File.Delete(FamilyFile("memory.aimodel"));
        var models = new FamilyModels(_content);
        await Assert.ThrowsAsync<InvalidDataException>(() => models.MemoryAsync());
        File.WriteAllBytes(FamilyFile("memory.aimodel"), saved);
        Assert.NotNull((await models.MemoryAsync()).Rnn);
    }

    [Fact]
    public async Task A_bad_file_gives_InvalidDataException()
    {
        File.WriteAllText(FamilyFile("memory.aimodel"), "not a model");
        var models = new FamilyModels(_content);
        await Assert.ThrowsAsync<InvalidDataException>(() => models.MemoryAsync());
    }

    [Fact]
    public async Task The_memory_file_is_read_once()
    {
        var models = new FamilyModels(_content);
        await models.MemoryAsync();
        File.Delete(FamilyFile("memory.aimodel"));   // a second read would now fail
        var again = await models.MemoryAsync();
        Assert.NotNull(again.Rnn);
    }

    [Fact]
    public async Task Each_diffusion_file_is_read_once()
    {
        var models = new FamilyModels(_content);
        await models.DiffusionAsync("Letter S");
        File.Delete(FamilyFile("diffusion-letter-s.aimodel"));
        Assert.Equal("Letter S", (await models.DiffusionAsync("Letter S")).ShapeName);
    }

    [Fact]
    public async Task Calls_at_the_same_time_share_one_read()
    {
        var models = new FamilyModels(_content);
        var both = await Task.WhenAll(models.MemoryAsync(), models.MemoryAsync());
        Assert.NotSame(both[0].Rnn, both[1].Rnn);
        File.Delete(FamilyFile("memory.aimodel"));
        Assert.NotNull((await models.MemoryAsync()).Rnn);
    }

    [Fact]
    public async Task LabFactory_builds_one_family_service_from_its_content_folder()
    {
        using var env = new TestEnvironment();
        var factory = new LabFactory(env.NewActiveModels(), TestEnvironment.Content);
        Assert.Same(factory.Family, factory.Family);
        Assert.Equal("Spiral", (await factory.Family.DiffusionAsync("Spiral")).ShapeName);
    }

    [Fact]
    public void Constructor_needs_a_folder()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FamilyModels(" "));
    }
}
