using System.Text.Json;
using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void LoadOrCreate_WhenNewConfigMissing_MigratesLegacyConfig()
    {
        using var temp = new TempDirectory();
        var newPath = Path.Combine(temp.Path, "new", "config.json");
        var legacyPath = Path.Combine(temp.Path, "legacy", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);

        var legacyConfig = new AppConfig
        {
            Enabled = false,
            Zone = new ZoneConfig
            {
                ActiveZone = new RectConfig { X = 12, Y = 34, Width = 56, Height = 78 }
            }
        };
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(legacyConfig));

        var loaded = ConfigStore.LoadOrCreate(newPath, legacyPath);

        Assert.False(loaded.Enabled);
        Assert.Equal(12, loaded.Zone.ActiveZone.X);
        Assert.True(File.Exists(newPath));
        Assert.True(File.Exists(legacyPath));
    }

    [Fact]
    public void LoadOrCreate_WhenConfigIsCorrupt_PreservesInvalidConfigBeforeDefaults()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        File.WriteAllText(path, "{ not valid json");

        var loaded = ConfigStore.LoadOrCreate(path, legacyPath: null);

        Assert.True(loaded.Enabled);
        Assert.True(File.Exists(path));
        var invalidConfigPath = Assert.Single(Directory.GetFiles(temp.Path, "config.json.*.invalid"));
        Assert.Equal("{ not valid json", File.ReadAllText(invalidConfigPath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"TaskbarUnhideZoner.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
