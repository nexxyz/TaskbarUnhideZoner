namespace TaskbarUnhideZoner.Runtime;

internal interface IZoneActivationHandler
{
    void OnZoneTriggered(Point cursorPosition);

    void OnZoneLeft();
}
