using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.UI;

internal static class OverlayHeatMap
{
    public static void NormalizeAssist(TriggerAssistConfig assist, out bool enabled, out double strength, out double curve)
    {
        enabled = assist.Enabled;
        strength = 1.0 - (Math.Clamp(assist.MinDelayPercent, 10, 100) / 100.0);
        curve = Math.Clamp(assist.CurveExponent, 0.35, 3.0);
    }

    public static double ComputeVisualHeat(double closeness, double assistStrength, double assistCurve)
    {
        var clampedCloseness = Math.Clamp(closeness, 0.0, 1.0);
        var boost = Math.Pow(clampedCloseness, assistCurve);
        var reduction = boost * assistStrength;
        return assistStrength > 0.001 ? reduction / assistStrength : 0.0;
    }

    public static Color BlendHeatColor(Color weakColor, Color strongColor, double visualHeat, int baseAlpha = 55, int alphaRange = 150)
    {
        var heat = Math.Clamp(visualHeat, 0.0, 1.0);
        var alpha = baseAlpha + (int)Math.Round(alphaRange * heat);
        var r = Lerp(weakColor.R, strongColor.R, heat);
        var g = Lerp(weakColor.G, strongColor.G, heat);
        var b = Lerp(weakColor.B, strongColor.B, heat);
        return Color.FromArgb(alpha, r, g, b);
    }

    private static int Lerp(int a, int b, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        return (int)Math.Round(a + ((b - a) * clamped));
    }
}
