namespace Kanban.Data;

public class MoveCardRequest
{
    public int TargetListId { get; set; }
    public int Position { get; set; }
}
