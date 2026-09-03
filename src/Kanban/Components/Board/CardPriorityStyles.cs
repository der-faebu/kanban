using Kanban.Data.Entities;

namespace Kanban.Components.Board;

public static class CardPriorityStyles
{
    public static string Label(CardPriority priority) => priority switch
    {
        CardPriority.Low => "Low",
        CardPriority.Medium => "Medium",
        CardPriority.High => "High",
        CardPriority.Urgent => "Urgent",
        _ => priority.ToString()
    };

    public static (string Fg, string Bg) Colors(CardPriority priority) => priority switch
    {
        CardPriority.Low => ("var(--knb-text-tertiary)", "var(--knb-bg-surface-3)"),
        CardPriority.Medium => ("var(--knb-accent)", "var(--knb-accent-subtle)"),
        CardPriority.High => ("var(--knb-warning)", "var(--knb-warning-subtle)"),
        CardPriority.Urgent => ("var(--knb-danger)", "var(--knb-danger-subtle)"),
        _ => ("var(--knb-text-tertiary)", "var(--knb-bg-surface-3)")
    };
}
