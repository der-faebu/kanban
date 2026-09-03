using Kanban.Data.Entities;

namespace Kanban.Components.Board;

public static class CardTypeStyles
{
    public static string Label(CardType type) => type switch
    {
        CardType.Bug => "Bug",
        CardType.Feature => "Feature",
        CardType.Task => "Task",
        CardType.Chore => "Chore",
        _ => type.ToString()
    };
}
