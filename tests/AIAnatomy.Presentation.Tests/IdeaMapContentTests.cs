using AIAnatomy.Lessons;
using AIAnatomy.Models.Language;

namespace AIAnatomy.Presentation.Tests;

public class IdeaMapContentTests
{
    [Fact]
    public void YourText_Description_StatesShippedContextLength()
    {
        using var stream = File.OpenRead(Path.Combine(TestEnvironment.Content, "models", "fables-small.aimodel"));
        int context = ModelFile.Load(stream).Transformer.Config.ContextLength;

        var map = IdeaMapData.Load(Path.Combine(TestEnvironment.Content, "idea-map.json"));
        var description = map.Nodes.Single(n => n.Id == "your-text").Description;

        Assert.Contains($"last {context} characters", description);
    }
}
