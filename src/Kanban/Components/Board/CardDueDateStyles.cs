using Kanban.Data.Entities;

namespace Kanban.Components.Board;

public static class CardDueDateStyles
{
    private const string NeutralFg = "var(--knb-text-secondary)";
    private const string NeutralBg = "var(--knb-bg-surface-3)";

    public static (string Fg, string Bg) Colors(DateTime dueDate, CardState state)
    {
        if (state == CardState.Done)
            return (NeutralFg, NeutralBg);

        var today = DateTime.UtcNow.Date;
        var due = dueDate.Date;

        if (due < today)
            return ("var(--knb-danger)", "var(--knb-danger-subtle)");

        if (due <= today.AddDays(2))
            return ("var(--knb-warning)", "var(--knb-warning-subtle)");

        return (NeutralFg, NeutralBg);
    }
}
