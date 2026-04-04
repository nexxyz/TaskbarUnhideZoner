namespace TaskbarUnhideZoner.Models;

internal sealed class AppConfig
{
    public bool Enabled { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public int TriggerDelayMs { get; set; } = 350;

    public int AutohideStatePollSeconds { get; set; } = 5;

    public DelayPresets DelayPresets { get; set; } = new();

    public ZoneConfig Zone { get; set; } = new();

    public TriggerConfig Trigger { get; set; } = new();

    public FullscreenConfig Fullscreen { get; set; } = new();

    public void Normalize()
    {
        DelayPresets ??= new DelayPresets();
        Zone ??= new ZoneConfig();
        Trigger ??= new TriggerConfig();
        Fullscreen ??= new FullscreenConfig();

        TriggerDelayMs = Math.Clamp(TriggerDelayMs, 50, 15000);
        AutohideStatePollSeconds = Math.Clamp(AutohideStatePollSeconds, 5, 300);
        DelayPresets.Normalize();
        Zone.Normalize();
        Trigger.Normalize();
    }
}

internal sealed class DelayPresets
{
    public int QuickMs { get; set; } = 180;
    public int DefaultMs { get; set; } = 350;
    public int LongMs { get; set; } = 700;

    public void Normalize()
    {
        QuickMs = Math.Clamp(QuickMs, 50, 15000);
        DefaultMs = Math.Clamp(DefaultMs, 50, 15000);
        LongMs = Math.Clamp(LongMs, 50, 15000);
    }
}

internal sealed class ZoneConfig
{
    public ZoneMode Mode { get; set; } = ZoneMode.HotZone;
    public RectConfig ActiveZone { get; set; } = new() { X = 0, Y = 0, Width = 320, Height = 120 };

    public void Normalize()
    {
        if (!Enum.IsDefined(typeof(ZoneMode), Mode))
        {
            Mode = ZoneMode.HotZone;
        }

        ActiveZone ??= new RectConfig { X = 0, Y = 0, Width = 320, Height = 120 };
        ActiveZone.Normalize();
    }
}

internal sealed class RectConfig
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 120;

    public void Normalize()
    {
        Width = Math.Clamp(Width, 1, 10000);
        Height = Math.Clamp(Height, 1, 10000);
    }
}

internal sealed class TriggerConfig
{
    public int CooldownMs { get; set; } = 500;
    public TriggerAssistConfig Assist { get; set; } = new();

    public void Normalize()
    {
        CooldownMs = Math.Clamp(CooldownMs, 0, 10000);
        Assist ??= new TriggerAssistConfig();
        Assist.Normalize();
    }
}

internal sealed class TriggerAssistConfig
{
    public bool Enabled { get; set; } = true;
    public int MinDelayPercent { get; set; } = 90;
    public double CurveExponent { get; set; } = 3.0;

    public void Normalize()
    {
        MinDelayPercent = Math.Clamp(MinDelayPercent, 10, 100);
        CurveExponent = Math.Clamp(CurveExponent, 0.35, 3.0);
    }
}

internal sealed class FullscreenConfig
{
    public bool SuspendWhenFullscreenAppActive { get; set; } = true;
}

internal enum EdgePosition
{
    Top,
    Bottom,
    Left,
    Right
}

internal enum ZoneMode
{
    Top,
    Bottom,
    Left,
    Right,
    HotZone
}

internal enum DelayPreset
{
    Quick,
    Default,
    Long
}

internal enum TriggerAssistPreset
{
    Off,
    Low,
    Medium,
    Strong
}
