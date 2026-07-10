namespace TaskbarUnhideZoner.Config;

internal static class Paths
{
    public static string AppDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskbarUnhideZoner");

    public static string LegacyAppDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TaskbarUnhideZoner");

    public static string ConfigFilePath => Path.Combine(AppDirectory, "config.json");

    public static string LegacyConfigFilePath => Path.Combine(LegacyAppDirectory, "config.json");

    public static string LogFilePath => Path.Combine(AppDirectory, "taskbar-unhide-zoner.log");
}
