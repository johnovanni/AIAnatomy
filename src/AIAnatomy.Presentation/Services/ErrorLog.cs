namespace AIAnatomy.Presentation.Services;

/// <summary>Records unexpected errors in a text file (<see cref="AppPaths.ErrorLogFile"/>) and words them for the user.</summary>
public static class ErrorLog
{
    /// <summary>Adds the time and the full exception to the end of the file. Returns false if it could not be written.</summary>
    public static bool Append(string file, Exception error, DateTimeOffset when)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            File.AppendAllText(file, $"--- {when:yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The plain message shown in the error window.</summary>
    public static string UserMessage(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        string message = error.Message.Trim().TrimEnd('.');
        return $"Something went wrong: {message}. Your settings and models are safe.";
    }
}
