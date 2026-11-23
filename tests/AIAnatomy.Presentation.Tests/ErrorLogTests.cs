using AIAnatomy.Presentation.Services;

namespace AIAnatomy.Presentation.Tests;

public class ErrorLogTests : IDisposable
{
    private readonly TestEnvironment _env = new();
    public void Dispose() => _env.Dispose();

    [Fact]
    public void Append_WritesTimeMessageAndDetails_AndKeepsOldEntries()
    {
        string file = Path.Combine(_env.Root, "sub", "error-log.txt");
        var when = new DateTimeOffset(2026, 11, 22, 14, 5, 0, TimeSpan.Zero);

        Assert.True(ErrorLog.Append(file, new InvalidOperationException("first"), when));
        Assert.True(ErrorLog.Append(file, new ArgumentException("second"), when));

        string text = File.ReadAllText(file);
        Assert.Contains("2026-11-22 14:05:00", text);
        Assert.Contains("System.InvalidOperationException: first", text);
        Assert.Contains("System.ArgumentException: second", text);
        Assert.True(text.IndexOf("first", StringComparison.Ordinal) < text.IndexOf("second", StringComparison.Ordinal));
    }

    [Fact]
    public void Append_WhenTheFileCannotBeWritten_ReturnsFalse()
    {
        Directory.CreateDirectory(Path.Combine(_env.Root, "error-log.txt"));   // a folder where the file should be
        Assert.False(ErrorLog.Append(Path.Combine(_env.Root, "error-log.txt"), new Exception("x"), DateTimeOffset.Now));
    }

    [Fact]
    public void Message_IsPlain()
    {
        Assert.Equal("Something went wrong: the disk is full. Your settings and models are safe.",
            ErrorLog.UserMessage(new IOException("the disk is full.")));
        Assert.Equal("Something went wrong: oops. Your settings and models are safe.", ErrorLog.UserMessage(new Exception("oops")));
    }
}
