namespace AIAnatomy.Lessons;

public static class SectionInfo
{
    public static int Number(Section section) => (int)section + 1;

    public static string Title(Section section) => section switch
    {
        Section.Map => "The big picture",
        Section.Parts => "The parts",
        Section.Whole => "The whole model",
        Section.Family => "The family of AI",
        Section.Beyond => "Beyond the model",
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };
}
