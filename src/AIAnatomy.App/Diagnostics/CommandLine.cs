using AIAnatomy.Presentation;

namespace AIAnatomy.App.Diagnostics;

/// <summary>Runs the diagnostic named by <see cref="DiagnosticOptions"/> (parsed in Presentation) and gives the exit code.</summary>
public static class CommandLine
{
    public static async Task<int> RunAsync(DiagnosticOptions options)
    {
        try
        {
            switch (options.Mode)
            {
                case DiagnosticMode.RenderPng:
                    await PngRenderer.RenderAsync(options);
                    return 0;
                case DiagnosticMode.SelfTest:
                    return await SelfTests.RunAsync(options);
                default:
                    throw new ArgumentException("Nothing to run.", nameof(options));
            }
        }
        catch (Exception e)   // a diagnostic run reports any failure on stderr and exits 1, never with a dialog
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }
}
