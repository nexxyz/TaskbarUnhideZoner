using System.Drawing;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Services;

namespace TaskbarUnhideZoner.Tests;

public sealed class TriggerAssistDelayTests
{
    [Fact]
    public void ComputeDelayMs_WhenAssistOff_ReturnsBaseDelay()
    {
        var config = CreateConfig(ZoneMode.Top, enabled: false, minDelayPercent: 10, curveExponent: 0.55);

        var delay = TriggerAssistDelay.ComputeDelayMs(config, new Point(100, 0));

        Assert.Equal(config.TriggerDelayMs, delay);
    }

    [Fact]
    public void ComputeDelayMs_TopMode_IsFasterNearTopEdge()
    {
        var config = CreateConfig(ZoneMode.Top, enabled: true, minDelayPercent: 10, curveExponent: 0.55);

        var nearTop = TriggerAssistDelay.ComputeDelayMs(config, new Point(100, 0));
        var nearBottom = TriggerAssistDelay.ComputeDelayMs(config, new Point(100, 119));

        Assert.True(nearTop < nearBottom);
        Assert.True(nearTop >= 35);
    }

    [Fact]
    public void ComputeDelayMs_HotZoneMode_IsFasterNearCenter()
    {
        var config = CreateConfig(ZoneMode.HotZone, enabled: true, minDelayPercent: 55, curveExponent: 1.7);

        var center = TriggerAssistDelay.ComputeDelayMs(config, new Point(160, 60));
        var corner = TriggerAssistDelay.ComputeDelayMs(config, new Point(0, 0));

        Assert.True(center < corner);
    }

    [Fact]
    public void ComputeDelayMs_StrongPresetRampsFasterThanLowPreset()
    {
        var low = CreateConfig(ZoneMode.Bottom, enabled: true, minDelayPercent: 90, curveExponent: 3.0);
        var strong = CreateConfig(ZoneMode.Bottom, enabled: true, minDelayPercent: 10, curveExponent: 0.55);

        var probe = new Point(160, 84);
        var lowDelay = TriggerAssistDelay.ComputeDelayMs(low, probe);
        var strongDelay = TriggerAssistDelay.ComputeDelayMs(strong, probe);

        Assert.True(strongDelay < lowDelay);
    }

    private static AppConfig CreateConfig(ZoneMode mode, bool enabled, int minDelayPercent, double curveExponent)
    {
        var config = new AppConfig
        {
            TriggerDelayMs = 350,
            Zone = new ZoneConfig
            {
                Mode = mode,
                ActiveZone = new RectConfig { X = 0, Y = 0, Width = 320, Height = 120 }
            },
            Trigger = new TriggerConfig
            {
                CooldownMs = 500,
                Assist = new TriggerAssistConfig
                {
                    Enabled = enabled,
                    MinDelayPercent = minDelayPercent,
                    CurveExponent = curveExponent
                }
            }
        };

        config.Normalize();
        return config;
    }
}
