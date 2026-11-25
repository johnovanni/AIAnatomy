using AIAnatomy.Models.Vision;

namespace AIAnatomy.Models.Tests;

public sealed class DigitSetStreamTests
{
    private static string Shipped(string name) => Path.Combine(AppContext.BaseDirectory, "content", "data", "digits", name);

    [Fact]
    public void Stream_overload_reads_the_same_set_as_the_path_overload()
    {
        var fromPath = DigitSet.Load(Shipped("digits-test.bin.gz"));
        using var stream = new MemoryStream(File.ReadAllBytes(Shipped("digits-test.bin.gz")));
        var fromStream = DigitSet.Load(stream, "digits-test.bin.gz");
        Assert.Equal(fromPath.Count, fromStream.Count);
        Assert.Equal(fromPath.ImageSize, fromStream.ImageSize);
        Assert.Equal(fromPath.Labels, fromStream.Labels);
        Assert.Equal(fromPath.Pixels(5), fromStream.Pixels(5));
    }

    [Fact]
    public void Stream_overload_wraps_errors_and_names_only_the_given_name()
    {
        var bytes = File.ReadAllBytes(Shipped("digits-test.bin.gz"));
        using var cut = new MemoryStream(bytes[..(bytes.Length / 2)]);
        var error = Assert.Throws<InvalidDataException>(() => DigitSet.Load(cut, "digits-test.bin.gz"));
        Assert.Contains("digits-test.bin.gz", error.Message);
        using var junk = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.Throws<InvalidDataException>(() => DigitSet.Load(junk, "x.bin.gz"));
    }

    [Fact]
    public void Path_overload_still_reports_a_missing_file_as_InvalidDataException()
    {
        var error = Assert.Throws<InvalidDataException>(() => DigitSet.Load(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid().ToString("N") + ".bin.gz")));
        Assert.Contains("nope-", error.Message);
    }
}
