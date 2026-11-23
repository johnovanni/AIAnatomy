namespace AIAnatomy.Presentation.Services;

/// <summary>Default folders. Only computes paths; nothing is created here.</summary>
public static class AppPaths
{
    public static string UserData { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AIAnatomy");

    public static string Content { get; } = Path.Combine(AppContext.BaseDirectory, "content");
    public static string ShippedModels { get; } = Path.Combine(Content, "models");
    public static string UserModels { get; } = Path.Combine(UserData, "models");
    public static string SettingsFile { get; } = Path.Combine(UserData, "settings.json");
    public static string ErrorLogFile { get; } = Path.Combine(UserData, "error-log.txt");
}
