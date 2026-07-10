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
        return LoadOrCreate(path, IsCurrentConfigPath(path) ? Paths.LegacyConfigFilePath : null);
    }

    internal static AppConfig LoadOrCreate(string path, string? legacyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.AppDirectory);

        if (!File.Exists(path))
        {
            TryMigrateLegacyConfig(path, legacyPath);
        }

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
            PreserveInvalidConfig(path);
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

    private static bool IsCurrentConfigPath(string path)
    {
        return string.Equals(Path.GetFullPath(path), Path.GetFullPath(Paths.ConfigFilePath), StringComparison.OrdinalIgnoreCase);
    }

    private static void TryMigrateLegacyConfig(string path, string? legacyPath)
    {
        if (string.IsNullOrWhiteSpace(legacyPath) || !File.Exists(legacyPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Paths.AppDirectory);
        File.Copy(legacyPath, path, overwrite: false);
    }

    private static void PreserveInvalidConfig(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var directory = Path.GetDirectoryName(path) ?? Paths.AppDirectory;
        var fileName = Path.GetFileName(path);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(directory, $"{fileName}.{timestamp}.invalid");
        var suffix = 0;

        while (File.Exists(backupPath))
        {
            suffix++;
            backupPath = Path.Combine(directory, $"{fileName}.{timestamp}.{suffix}.invalid");
        }

        File.Move(path, backupPath);
    }
}
