namespace Kanban.Data;

public class ReorderListsRequest
{
    public List<(int ListId, int Position)> Positions { get; set; } = [];
}
