namespace Kanban.Data;

public class ReorderCardsRequest
{
    public List<(int CardId, int Position)> Positions { get; set; } = [];
}
