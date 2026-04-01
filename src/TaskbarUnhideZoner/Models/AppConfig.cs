namespace TaskbarUnhideZoner.Models;

internal sealed class AppConfig
{
    public bool Enabled { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public int TriggerDelayMs { get; set; } = 350;

    public DelayPresets DelayPresets { get; set; } = new();

    public ZoneConfig Zone { get; set; } = new();

    public DetectionConfig Detection { get; set; } = new();

    public TriggerConfig Trigger { get; set; } = new();

    public FullscreenConfig Fullscreen { get; set; } = new();

    public void Normalize()
    {
        DelayPresets ??= new DelayPresets();
        Zone ??= new ZoneConfig();
        Detection ??= new DetectionConfig();
        Trigger ??= new TriggerConfig();
        Fullscreen ??= new FullscreenConfig();

        TriggerDelayMs = Math.Clamp(TriggerDelayMs, 50, 15000);
        DelayPresets.Normalize();
        Zone.Normalize();
        Detection.Normalize();
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
    public ZoneMode Mode { get; set; } = ZoneMode.EdgeBar;
    public EdgePosition Edge { get; set; } = EdgePosition.Bottom;
    public int EdgeThicknessPx { get; set; } = 2;
    public RectConfig HotZone { get; set; } = new() { X = 0, Y = 0, Width = 320, Height = 120 };

    public void Normalize()
    {
        EdgeThicknessPx = Math.Clamp(EdgeThicknessPx, 1, 100);
        HotZone ??= new RectConfig { X = 0, Y = 0, Width = 320, Height = 120 };
        HotZone.Normalize();
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

internal sealed class DetectionConfig
{
    public DetectionBackend Backend { get; set; } = DetectionBackend.Auto;
    public int PollIntervalMs { get; set; } = 33;

    public void Normalize()
    {
        PollIntervalMs = Math.Clamp(PollIntervalMs, 10, 250);
    }
}

internal sealed class TriggerConfig
{
    public TriggerStrategy Strategy { get; set; } = TriggerStrategy.NoMoveThenNudge;
    public int NudgePx { get; set; } = 1;
    public int CooldownMs { get; set; } = 500;

    public void Normalize()
    {
        NudgePx = Math.Clamp(NudgePx, 1, 5);
        CooldownMs = Math.Clamp(CooldownMs, 0, 10000);
    }
}

internal sealed class FullscreenConfig
{
    public bool SuspendWhenFullscreenAppActive { get; set; } = true;
}

internal enum ZoneMode
{
    EdgeBar,
    HotZone
}

internal enum EdgePosition
{
    Top,
    Bottom,
    Left,
    Right
}

internal enum DetectionBackend
{
    Auto,
    MouseHook,
    Polling
}

internal enum TriggerStrategy
{
    NoMoveThenNudge
}

internal enum DelayPreset
{
    Quick,
    Default,
    Long
}
