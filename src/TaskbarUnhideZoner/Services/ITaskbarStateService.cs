namespace TaskbarUnhideZoner.Services;

internal interface ITaskbarStateService
{
    bool IsAutoHideEnabled();

    // Only used to recover from app versions before 1.1, which turned auto-hide off while revealing.
    bool EnableAutoHide();
}
