using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Vision;
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
        var data = Path.Combine(_content, "data", "digits");
        Directory.CreateDirectory(data);
        foreach (var file in Directory.GetFiles(Path.Combine(TestEnvironment.Content, "data", "digits"), "*.bin.gz"))
            File.Copy(file, Path.Combine(data, Path.GetFileName(file)));
    }

    public void Dispose()
    {
        try { Directory.Delete(_content, recursive: true); }
        catch (IOException) { /* temp folder */ }
    }

    private string FamilyFile(string name) => Path.Combine(_content, "models", "family", name);

    private string DigitsFile(string name) => Path.Combine(_content, "data", "digits", name);

    private static int Predict(DigitNet net, DigitSet set, int index)
    {
        var probabilities = net.Forward(set.Batch(new[] { index })).Probabilities(0);
        return Array.IndexOf(probabilities, probabilities.Max());
    }

    [Fact]
    public async Task DigitsAsync_returns_a_network_that_classifies_like_DigitNet_directly()
    {
        var models = new FamilyModels(_content);
        var (net, info) = await models.DigitsAsync();
        Assert.True(info.IsShipped);
        Assert.Equal(8, net.Config.ImageSize);
        DigitNet direct;
        using (var stream = File.OpenRead(FamilyFile("digits.aimodel"))) direct = DigitNet.Load(stream).Net;
        var test = await models.DigitTestSetAsync();
        Assert.Equal(Predict(direct, test, 0), Predict(net, test, 0));
        Assert.Equal(direct.Forward(test.Batch(new[] { 0 })).Logits.Data, net.Forward(test.Batch(new[] { 0 })).Logits.Data);
    }

    [Fact]
    public async Task Two_digit_calls_give_independent_networks()
    {
        var models = new FamilyModels(_content);
        var first = (await models.DigitsAsync()).Net;
        var second = (await models.DigitsAsync()).Net;
        Assert.NotSame(first, second);
        var firstWeights = first.Parameters().First().Value.Data;
        var secondWeights = second.Parameters().First().Value.Data;
        double before = secondWeights[0];
        firstWeights[0] += 1.0;
        Assert.Equal(before, secondWeights[0]);
    }

    [Fact]
    public async Task The_digit_sets_load_with_the_expected_counts()
    {
        var models = new FamilyModels(_content);
        var test = await models.DigitTestSetAsync();
        var train = await models.DigitTrainSetAsync();
        Assert.Equal(1740, test.Count);
        Assert.Equal(3760, train.Count);
        Assert.Equal(8, test.ImageSize);
        Assert.Equal(8, train.ImageSize);
    }

    [Fact]
    public async Task A_missing_digits_file_gives_a_plain_message()
    {
        File.Delete(FamilyFile("digits.aimodel"));
        File.Delete(DigitsFile("digits-test.bin.gz"));
        var models = new FamilyModels(_content);
        var net = await Assert.ThrowsAsync<InvalidDataException>(() => models.DigitsAsync());
        Assert.Contains("digits.aimodel", net.Message);
        Assert.DoesNotContain(_content, net.Message);
        var set = await Assert.ThrowsAsync<InvalidDataException>(() => models.DigitTestSetAsync());
        Assert.Contains("digits-test.bin.gz", set.Message);
        Assert.DoesNotContain(_content, set.Message);
        Assert.Equal(3760, (await models.DigitTrainSetAsync()).Count);
    }

    [Fact]
    public async Task A_truncated_digits_file_gives_a_plain_message()
    {
        var bytes = File.ReadAllBytes(FamilyFile("digits.aimodel"));
        File.WriteAllBytes(FamilyFile("digits.aimodel"), bytes[..(bytes.Length / 2)]);
        var set = File.ReadAllBytes(DigitsFile("digits-train.bin.gz"));
        File.WriteAllBytes(DigitsFile("digits-train.bin.gz"), set[..(set.Length / 2)]);
        var models = new FamilyModels(_content);
        var net = await Assert.ThrowsAsync<InvalidDataException>(() => models.DigitsAsync());
        Assert.Contains("digits.aimodel", net.Message);
        Assert.DoesNotContain(_content, net.Message);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.DigitTrainSetAsync());
        Assert.Contains("digits-train.bin.gz", error.Message);
        Assert.DoesNotContain(_content, error.Message);
    }

    [Fact]
    public async Task A_failed_digit_set_read_is_tried_again()
    {
        var saved = File.ReadAllBytes(DigitsFile("digits-test.bin.gz"));
        File.Delete(DigitsFile("digits-test.bin.gz"));
        var models = new FamilyModels(_content);
        await Assert.ThrowsAsync<InvalidDataException>(() => models.DigitTestSetAsync());
        File.WriteAllBytes(DigitsFile("digits-test.bin.gz"), saved);
        Assert.Equal(1740, (await models.DigitTestSetAsync()).Count);
    }

    [Fact]
    public async Task Each_digit_file_is_read_once()
    {
        var read = new List<string>();
        var models = new FamilyModels(_content, path => { lock (read) read.Add(Path.GetFileName(path)); return File.ReadAllBytes(path); });
        await models.DigitsAsync();
        await models.DigitsAsync();
        await Task.WhenAll(models.DigitTestSetAsync(), models.DigitTestSetAsync(), models.DigitTrainSetAsync());
        await models.DigitTestSetAsync();
        await models.DigitTrainSetAsync();
        Assert.Equal(new[] { "digits.aimodel", "digits-test.bin.gz", "digits-train.bin.gz" }, read.OrderBy(f => f == "digits.aimodel" ? 0 : f == "digits-test.bin.gz" ? 1 : 2));
    }

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
        int reads = 0;
        using var gate = new ManualResetEventSlim(false);
        var models = new FamilyModels(_content, path =>
        {
            Interlocked.Increment(ref reads);
            gate.Wait(TimeSpan.FromSeconds(10));   // hold the read until both calls have been made
            return File.ReadAllBytes(path);
        });
        var first = models.MemoryAsync();
        var second = models.MemoryAsync();
        gate.Set();
        var both = await Task.WhenAll(first, second);
        Assert.NotSame(both[0].Rnn, both[1].Rnn);
        await models.MemoryAsync();
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Each_file_is_read_once_by_count()
    {
        var read = new List<string>();
        var models = new FamilyModels(_content, path => { lock (read) read.Add(Path.GetFileName(path)); return File.ReadAllBytes(path); });
        await models.DiffusionAsync("Spiral");
        await models.DiffusionAsync("Spiral");
        await models.DiffusionAsync("Circle");
        await models.MemoryAsync();
        await models.MemoryAsync();
        Assert.Equal(new[] { "diffusion-spiral.aimodel", "diffusion-circle.aimodel", "memory.aimodel" }, read);
    }

    [Fact]
    public async Task A_truncated_file_gives_a_plain_message()
    {
        var bytes = File.ReadAllBytes(FamilyFile("memory.aimodel"));
        File.WriteAllBytes(FamilyFile("memory.aimodel"), bytes[..(bytes.Length / 2)]);
        var models = new FamilyModels(_content);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.MemoryAsync());
        Assert.Contains("memory.aimodel", error.Message);
        Assert.DoesNotContain(_content, error.Message);
    }

    [Fact]
    public async Task A_read_that_ends_early_is_wrapped()
    {
        var models = new FamilyModels(_content, _ => throw new EndOfStreamException("raw"));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.DiffusionAsync("Circle"));
        Assert.Contains("diffusion-circle.aimodel", error.Message);
        Assert.DoesNotContain("raw", error.Message);
    }

    [Fact]
    public async Task An_unreadable_file_gives_a_plain_message_without_the_path()
    {
        var models = new FamilyModels(_content, path => throw new UnauthorizedAccessException($"Access to the path '{path}' is denied."));
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => models.MemoryAsync());
        Assert.Contains("memory.aimodel", error.Message);
        Assert.DoesNotContain(_content, error.Message);
    }

    [Fact]
    public async Task A_second_call_after_a_bad_file_also_fails_cleanly()
    {
        File.WriteAllText(FamilyFile("diffusion-spiral.aimodel"), "not a model");
        var models = new FamilyModels(_content);
        var first = await Assert.ThrowsAsync<InvalidDataException>(() => models.DiffusionAsync("Spiral"));
        var second = await Assert.ThrowsAsync<InvalidDataException>(() => models.DiffusionAsync("Spiral"));
        Assert.Equal(first.Message, second.Message);
        Assert.Contains("diffusion-spiral.aimodel", second.Message);
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
