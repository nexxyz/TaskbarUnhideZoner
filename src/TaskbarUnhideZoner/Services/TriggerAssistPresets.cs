using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.Services;

internal static class TriggerAssistPresets
{
    private const double CurveTolerance = 0.001;

    public static void Apply(TriggerAssistConfig assist, TriggerAssistPreset preset)
    {
        switch (preset)
        {
            case TriggerAssistPreset.Off:
                assist.Enabled = false;
                assist.MinDelayPercent = 100;
                assist.CurveExponent = 1.0;
                return;

            case TriggerAssistPreset.Low:
                assist.Enabled = true;
                assist.MinDelayPercent = 90;
                assist.CurveExponent = 3.0;
                return;

            case TriggerAssistPreset.Medium:
                assist.Enabled = true;
                assist.MinDelayPercent = 60;
                assist.CurveExponent = 1.7;
                return;

            case TriggerAssistPreset.Strong:
                assist.Enabled = true;
                assist.MinDelayPercent = 10;
                assist.CurveExponent = 0.55;
                return;

            default:
                assist.Enabled = true;
                assist.MinDelayPercent = 90;
                assist.CurveExponent = 3.0;
                return;
        }
    }

    public static TriggerAssistPreset? DetectExact(TriggerAssistConfig assist)
    {
        if (!assist.Enabled)
        {
            return TriggerAssistPreset.Off;
        }

        if (IsMatch(assist, 90, 3.0))
        {
            return TriggerAssistPreset.Low;
        }

        if (IsMatch(assist, 60, 1.7))
        {
            return TriggerAssistPreset.Medium;
        }

        if (IsMatch(assist, 10, 0.55))
        {
            return TriggerAssistPreset.Strong;
        }

        return null;
    }

    private static bool IsMatch(TriggerAssistConfig assist, int minDelayPercent, double curveExponent)
    {
        return assist.MinDelayPercent == minDelayPercent && Math.Abs(assist.CurveExponent - curveExponent) < CurveTolerance;
    }
}
