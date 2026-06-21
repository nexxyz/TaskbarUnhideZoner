namespace TaskbarUnhideZoner.Services;

internal interface ITaskbarStateService
{
    uint GetStateFlags();

    bool IsAutoHideEnabled();

    bool SetAutoHideEnabled(bool enabled);

    bool SetStateFlags(uint stateFlags);
}
