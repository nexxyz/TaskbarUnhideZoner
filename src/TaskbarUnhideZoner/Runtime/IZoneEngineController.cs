namespace TaskbarUnhideZoner.Runtime;

internal interface IZoneEngineController : IDisposable
{
    void Start();

    void Stop();

    void Reinitialize();
}
