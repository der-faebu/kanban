namespace Kanban.Services;

// Scoped per Blazor Server circuit (one per connected user), not a singleton --
// each user's theme choice must stay independent of every other user's.
public class ThemeState
{
    public bool IsDarkMode { get; private set; }
    public event Action? Changed;

    public void SetDarkMode(bool isDarkMode)
    {
        if (IsDarkMode == isDarkMode)
            return;

        IsDarkMode = isDarkMode;
        Changed?.Invoke();
    }
}
