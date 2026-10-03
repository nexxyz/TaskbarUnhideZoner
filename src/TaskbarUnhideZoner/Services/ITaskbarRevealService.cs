namespace TaskbarUnhideZoner.Services;

internal interface ITaskbarRevealService
{
    // Returns false when nothing was sent because Explorer already keeps the taskbars shown.
    bool Reveal();

    bool IsAnyTaskbarShown();
}
