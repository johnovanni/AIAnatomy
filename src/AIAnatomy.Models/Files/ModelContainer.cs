using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIAnatomy.Models.Files;

/// <summary>
/// The parts every .aimodel file shares (D14): one UTF-8 JSON header line ending in "\n", then every parameter's numbers
/// as little-endian 64-bit floats, written and read in blocks of 4096. <see cref="Language.ModelFile"/> (version 1) and
/// <see cref="FamilyModelFile"/> (version 2) each define their own header record on top of this.
/// </summary>
internal static class ModelContainer
{
    public const string FormatName = "aianatomy-model";
    public const int LanguageVersion = 1;
    public const int FamilyVersion = 2;
    public const int MaxHeaderBytes = 4 * 1024 * 1024;
    public const int MaxDimension = 100_000;
    public const long MaxParameters = 50_000_000;
    private const int Block = 4096;

    public static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public sealed record ParameterEntry(string Name, int[] Shape);

    /// <summary>The first fields of any version's header, read before deciding which reader applies.</summary>
    public sealed record Preamble(string? Format, int Version, string? Kind);

    public static void WriteHeader<THeader>(Stream s, THeader header) =>
        s.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header, Json) + "\n"));

    public static void WriteValues(Stream s, IEnumerable<double[]> blocks)
    {
        var buffer = new byte[8 * Block];
        foreach (var data in blocks)
        {
            for (int offset = 0; offset < data.Length; offset += Block)
            {
                int count = Math.Min(Block, data.Length - offset);
                for (int i = 0; i < count; i++) BinaryPrimitives.WriteDoubleLittleEndian(buffer.AsSpan(i * 8), data[offset + i]);
                s.Write(buffer, 0, count * 8);
            }
        }
    }

    public static byte[] ReadHeaderLine(Stream s)
    {
        var bytes = new List<byte>();
        while (true)
        {
            int b = s.ReadByte();
            if (b < 0) throw new InvalidDataException("The model file ends before its header does.");
            if (b == '\n') return bytes.ToArray();
            bytes.Add((byte)b);
            if (bytes.Count > MaxHeaderBytes) throw new InvalidDataException("The model file does not start with a readable header.");
        }
    }

    /// <summary>Reads format, version and kind, and checks the format. The caller decides what the version means.</summary>
    public static Preamble ReadPreamble(byte[] line)
    {
        var preamble = JsonSerializer.Deserialize<Preamble>(line, Json)
            ?? throw new InvalidDataException("The model file does not start with a readable header.");
        if (preamble.Format != FormatName) throw new InvalidDataException("This is not an AI Anatomy model file.");
        return preamble;
    }

    public static THeader ReadHeader<THeader>(byte[] line) where THeader : class =>
        JsonSerializer.Deserialize<THeader>(line, Json) ?? throw new InvalidDataException("The model file does not start with a readable header.");

    public static string VersionMessage(int found, int reads) => $"This model file has version {found}, but this program reads version {reads}.";

    /// <summary>Fills <paramref name="data"/> from the stream; any number that is not finite is refused.</summary>
    public static void ReadValues(Stream s, string name, double[] data, byte[] buffer)
    {
        for (int offset = 0; offset < data.Length; offset += Block)
        {
            int count = Math.Min(Block, data.Length - offset);
            s.ReadExactly(buffer, 0, count * 8);
            for (int i = 0; i < count; i++)
            {
                double value = BinaryPrimitives.ReadDoubleLittleEndian(buffer.AsSpan(i * 8));
                if (!double.IsFinite(value)) throw new InvalidDataException($"The model file has a number that is not valid in '{name}'.");
                data[offset + i] = value;
            }
        }
    }

    public static byte[] NewBuffer() => new byte[8 * Block];

    public static void ExpectEnd(Stream s)
    {
        if (s.ReadByte() != -1) throw new InvalidDataException("The model file has extra data after its numbers.");
    }

    /// <summary>Runs a reader and turns the ways a damaged file can fail into <see cref="InvalidDataException"/> with a plain message.</summary>
    public static T Read<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("The model file is cut short: some of its numbers are missing.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The model file does not start with a readable header.");
        }
        catch (Exception e) when (e is ArgumentException or OverflowException or NotSupportedException)
        {
            throw new InvalidDataException("The model file describes a model that cannot be built: " + e.Message);
        }
    }
}
