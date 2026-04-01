namespace TaskbarUnhideZoner.Detection;

internal interface IZoneMonitor : IDisposable
{
    string Name { get; }

    event EventHandler<CursorPositionEventArgs>? CursorPositionChanged;

    void Start();

    void Stop();
}
