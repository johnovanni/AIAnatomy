using System.IO.Compression;
using System.Text;
using AIAnatomy.Core;

namespace AIAnatomy.Models.Vision;

/// <summary>
/// A set of small greyscale digit images with labels 0-9. Stored gzip-compressed: ASCII "AIDIGITS", then int32 version (1),
/// count and imageSize (little-endian), then per image one label byte and imageSize squared pixel bytes (0 = background).
/// </summary>
public sealed class DigitSet
{
    private const string Magic = "AIDIGITS";
    private const int Version = 1;
    private const int MaxImageSize = 256;

    private readonly byte[] _pixels;   // count * ImageSize^2, row by row
    private readonly int[] _labels;

    private DigitSet(int imageSize, int[] labels, byte[] pixels)
    {
        ImageSize = imageSize;
        _labels = labels;
        _pixels = pixels;
    }

    public int Count => _labels.Length;
    public int ImageSize { get; }
    public IReadOnlyList<int> Labels => _labels;

    public int Label(int index) => _labels[CheckIndex(index)];

    /// <summary>The image's pixels scaled to 0..1, row by row.</summary>
    public double[] Pixels(int index)
    {
        int n = ImageSize * ImageSize;
        var result = new double[n];
        Fill(result, 0, CheckIndex(index));
        return result;
    }

    /// <summary>A tensor of shape [N, 1, ImageSize, ImageSize] holding the chosen images.</summary>
    public Tensor Batch(IReadOnlyList<int> indices)
    {
        int n = ImageSize * ImageSize;
        var data = new double[indices.Count * n];
        for (int i = 0; i < indices.Count; i++) Fill(data, i * n, CheckIndex(indices[i]));
        return Tensor.FromArray(data, indices.Count, 1, ImageSize, ImageSize);
    }

    private void Fill(double[] target, int offset, int index)
    {
        int n = ImageSize * ImageSize;
        for (int j = 0; j < n; j++) target[offset + j] = _pixels[index * n + j] / 255.0;
    }

    private int CheckIndex(int index)
        => (uint)index < (uint)Count ? index : throw new ArgumentOutOfRangeException(nameof(index), $"Image {index} is outside 0..{Count - 1}.");

    public static void Save(string path, int imageSize, IReadOnlyList<(byte Label, byte[] Pixels)> images)
    {
        if (imageSize < 1 || imageSize > MaxImageSize) throw new ArgumentException($"Image size {imageSize} is not in 1..{MaxImageSize}.", nameof(imageSize));
        for (int i = 0; i < images.Count; i++)
        {
            if (images[i].Label > 9) throw new ArgumentException($"Image {i} has label {images[i].Label}; labels are 0-9.", nameof(images));
            if (images[i].Pixels.Length != imageSize * imageSize)
                throw new ArgumentException($"Image {i} has {images[i].Pixels.Length} pixels; {imageSize}x{imageSize} needs {imageSize * imageSize}.", nameof(images));
        }
        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (dir is not null) Directory.CreateDirectory(dir);
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        using var w = new BinaryWriter(gzip, Encoding.ASCII);
        w.Write(Encoding.ASCII.GetBytes(Magic));
        w.Write(Version);
        w.Write(images.Count);
        w.Write(imageSize);
        foreach (var (label, pixels) in images)
        {
            w.Write(label);
            w.Write(pixels);
        }
    }

    public static DigitSet Load(string path)
    {
        string name = Path.GetFileName(path);
        try
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var r = new BinaryReader(gzip, Encoding.ASCII);

            if (Encoding.ASCII.GetString(ReadExactly(r, 8)) != Magic) throw new InvalidDataException("it is not a digits file (wrong magic)");
            int version = r.ReadInt32();
            if (version != Version) throw new InvalidDataException($"unsupported version {version}");
            int count = r.ReadInt32();
            int size = r.ReadInt32();
            if (size < 1 || size > MaxImageSize) throw new InvalidDataException($"image size {size} is out of range");
            if (count < 0 || count > 1_000_000) throw new InvalidDataException($"image count {count} is out of range");

            int n = size * size;
            var labels = new int[count];
            var pixels = new byte[count * n];
            for (int i = 0; i < count; i++)
            {
                byte label = ReadExactly(r, 1)[0];
                if (label > 9) throw new InvalidDataException($"image {i} has label {label}, outside 0-9");
                labels[i] = label;
                ReadExactly(r, n).CopyTo(pixels, i * n);
            }
            if (r.Read() != -1) throw new InvalidDataException("it has extra bytes after the last image");
            return new DigitSet(size, labels, pixels);
        }
        catch (InvalidDataException e)
        {
            throw new InvalidDataException($"Digits file '{name}' is not valid: {e.Message}.", e);
        }
        catch (EndOfStreamException e)
        {
            throw new InvalidDataException($"Digits file '{name}' is not valid: it ends early.", e);
        }
        catch (IOException e)
        {
            throw new InvalidDataException($"Digits file '{name}' could not be read: {e.Message}", e);
        }
        catch (UnauthorizedAccessException e)
        {
            throw new InvalidDataException($"Digits file '{name}' could not be read: {e.Message}", e);
        }
    }

    private static byte[] ReadExactly(BinaryReader r, int count)
    {
        var bytes = r.ReadBytes(count);
        if (bytes.Length != count) throw new EndOfStreamException();
        return bytes;
    }
}
