using System.Text.Json;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Config;

internal static class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static AppConfig LoadOrCreate(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.AppDirectory);

        if (!File.Exists(path))
        {
            var defaults = new AppConfig();
            Save(path, defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            config.Normalize();
            return config;
        }
        catch
        {
            var fallback = new AppConfig();
            Save(path, fallback);
            return fallback;
        }
    }

    public static void Save(string path, AppConfig config)
    {
        config.Normalize();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.AppDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }
}
