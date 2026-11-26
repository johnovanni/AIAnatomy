using System.IO.Compression;
using System.Text;
using AIAnatomy.Models.Vision;

namespace AIAnatomy.Models.Tests.Vision;

public class DigitSetTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "digitset-" + Guid.NewGuid().ToString("N"));

    public DigitSetTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private string P(string name) => Path.Combine(_dir, name);

    private static string ContentPath(string file) => Path.Combine(AppContext.BaseDirectory, "content", "data", "digits", file);

    private static List<(byte Label, byte[] Pixels)> Sample(int size = 3)
    {
        var list = new List<(byte, byte[])>();
        for (int i = 0; i < 5; i++)
        {
            var px = new byte[size * size];
            for (int j = 0; j < px.Length; j++) px[j] = (byte)((i * 40 + j * 7) % 256);
            list.Add(((byte)(i * 2 % 10), px));
        }
        return list;
    }

    /// <summary>Writes a gzip file from raw bytes, so a test can build a malformed one.</summary>
    private string WriteRaw(string name, byte[] raw)
    {
        string path = P(name);
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.Optimal);
        gz.Write(raw);
        return path;
    }

    private static byte[] Raw(int version, int count, int size, IEnumerable<(byte, byte[])> images, bool omitLastByte = false, int trailing = 0)
    {
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m);
        w.Write(Encoding.ASCII.GetBytes("AIDIGITS"));
        w.Write(version); w.Write(count); w.Write(size);
        foreach (var (l, px) in images) { w.Write(l); w.Write(px); }
        for (int i = 0; i < trailing; i++) w.Write((byte)0);
        w.Flush();
        var bytes = m.ToArray();
        return omitLastByte ? bytes[..^1] : bytes;
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var images = Sample();
        string path = P("a.bin.gz");
        DigitSet.Save(path, 3, images);
        var set = DigitSet.Load(path);
        Assert.Equal(5, set.Count);
        Assert.Equal(3, set.ImageSize);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(images[i].Label, set.Label(i));
            Assert.Equal(images[i].Pixels.Select(b => b / 255.0), set.Pixels(i));
        }
        Assert.Equal(images.Select(x => (int)x.Label), set.Labels);
        // Final review M14: the sets are shared across labs, so the labels cannot be changed through this list.
        Assert.False(set.Labels is int[], "Labels hands out the set's own array");
        Assert.True(((ICollection<int>)set.Labels).IsReadOnly);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)set.Labels)[0] = 9);
        Assert.Equal(images[0].Label, set.Label(0));
    }

    [Fact]
    public void Load_WrongMagic_NamesTheFile()
    {
        var raw = Raw(1, 1, 2, new[] { ((byte)1, new byte[4]) });
        raw[0] = (byte)'X';
        string path = WriteRaw("magic.bin.gz", raw);
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("magic.bin.gz", e.Message);
    }

    [Fact]
    public void Load_LabelOutsideRange_NamesTheFile()
    {
        string path = WriteRaw("label.bin.gz", Raw(1, 1, 2, new[] { ((byte)10, new byte[4]) }));
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("label.bin.gz", e.Message);
    }

    [Fact]
    public void Load_ShortFile_NamesTheFile()
    {
        string path = WriteRaw("short.bin.gz", Raw(1, 2, 2, new[] { ((byte)1, new byte[4]), ((byte)2, new byte[4]) }, omitLastByte: true));
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("short.bin.gz", e.Message);
    }

    [Fact]
    public void Load_TrailingBytes_NamesTheFile()
    {
        string path = WriteRaw("trail.bin.gz", Raw(1, 1, 2, new[] { ((byte)1, new byte[4]) }, trailing: 1));
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("trail.bin.gz", e.Message);
    }

    [Fact]
    public void Load_BadVersion_NamesTheFile()
    {
        string path = WriteRaw("ver.bin.gz", Raw(2, 1, 2, new[] { ((byte)1, new byte[4]) }));
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("ver.bin.gz", e.Message);
    }

    [Fact]
    public void Load_NotGzip_NamesTheFile()
    {
        string path = P("plain.bin.gz");
        File.WriteAllText(path, "this is not gzip");
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(path));
        Assert.Contains("plain.bin.gz", e.Message);
    }

    [Fact]
    public void Load_MissingFile_NamesTheFile()
    {
        var e = Assert.Throws<InvalidDataException>(() => DigitSet.Load(P("nothing.bin.gz")));
        Assert.Contains("nothing.bin.gz", e.Message);
    }

    [Fact]
    public void Save_RejectsBadLabelAndBadPixelCount()
    {
        Assert.Throws<ArgumentException>(() => DigitSet.Save(P("x"), 2, new[] { ((byte)10, new byte[4]) }));
        Assert.Throws<ArgumentException>(() => DigitSet.Save(P("y"), 2, new[] { ((byte)1, new byte[3]) }));
    }

    [Fact]
    public void Batch_HasShapeAndValues()
    {
        var images = Sample();
        string path = P("b.bin.gz");
        DigitSet.Save(path, 3, images);
        var set = DigitSet.Load(path);
        var batch = set.Batch(new[] { 3, 1 });
        Assert.Equal(new[] { 2, 1, 3, 3 }, batch.Shape);
        for (int j = 0; j < 9; j++)
        {
            Assert.Equal(images[3].Pixels[j] / 255.0, batch.Data[j]);
            Assert.Equal(images[1].Pixels[j] / 255.0, batch.Data[9 + j]);
        }
    }

    [Fact]
    public void Batch_IndexOutOfRange_Throws()
    {
        string path = P("c.bin.gz");
        DigitSet.Save(path, 3, Sample());
        var set = DigitSet.Load(path);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Batch(new[] { 5 }));
    }

    [Theory]
    [InlineData("digits-train.bin.gz", 3760)]
    [InlineData("digits-test.bin.gz", 1740)]
    public void ShippedSubset_IsBalanced_AndEightByEight(string file, int expected)
    {
        var set = DigitSet.Load(ContentPath(file));
        Assert.Equal(8, set.ImageSize);
        Assert.Equal(expected, set.Count);
        var perDigit = Enumerable.Range(0, 10).Select(d => set.Labels.Count(l => l == d)).ToArray();
        Assert.All(perDigit, c => Assert.Equal(expected / 10, c));
    }

    [Fact]
    public void ShippedSubsets_NoTestImageIsIdenticalToATrainingImage()
    {
        var train = DigitSet.Load(ContentPath("digits-train.bin.gz"));
        var test = DigitSet.Load(ContentPath("digits-test.bin.gz"));
        var seen = new HashSet<string>(Enumerable.Range(0, train.Count).Select(i => Convert.ToBase64String(Bytes(train, i))));
        int clashes = Enumerable.Range(0, test.Count).Count(i => seen.Contains(Convert.ToBase64String(Bytes(test, i))));
        Assert.Equal(0, clashes);
    }

    private static byte[] Bytes(DigitSet set, int i) => set.Pixels(i).Select(v => (byte)Math.Round(v * 255)).ToArray();
}
