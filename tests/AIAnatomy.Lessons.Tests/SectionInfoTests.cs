namespace AIAnatomy.Lessons.Tests;

public class SectionInfoTests
{
    [Fact]
    public void Numbers_RunOneToFive()
    {
        Assert.Equal([1, 2, 3, 4, 5], Enum.GetValues<Section>().Select(SectionInfo.Number).ToArray());
    }

    [Fact]
    public void Titles_AreThePlannedNames()
    {
        Assert.Equal(["The big picture", "The parts", "The whole model", "The family of AI", "Beyond the model"],
            Enum.GetValues<Section>().Select(SectionInfo.Title).ToArray());
    }
}
