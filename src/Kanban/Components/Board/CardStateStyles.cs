using Kanban.Data.Entities;

namespace Kanban.Components.Board;

public static class CardStateStyles
{
    public static string Label(CardState state) => state switch
    {
        CardState.NotStarted => "Not started",
        CardState.InProgress => "In progress",
        CardState.Done => "Done",
        _ => state.ToString()
    };

    public static (string Fg, string Bg) Colors(CardState state) => state switch
    {
        CardState.NotStarted => ("var(--knb-text-tertiary)", "var(--knb-bg-surface-3)"),
        CardState.InProgress => ("var(--knb-accent)", "var(--knb-accent-subtle)"),
        CardState.Done => ("var(--knb-success)", "var(--knb-success-subtle)"),
        _ => ("var(--knb-text-tertiary)", "var(--knb-bg-surface-3)")
    };
}
