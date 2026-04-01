namespace TaskbarUnhideZoner.Detection;

internal sealed class CursorPositionEventArgs : EventArgs
{
    public CursorPositionEventArgs(System.Drawing.Point position, long elapsedMs)
    {
        Position = position;
        ElapsedMs = elapsedMs;
    }

    public System.Drawing.Point Position { get; }

    public long ElapsedMs { get; }
}
