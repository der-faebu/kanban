namespace Kanban.Services;

public class DragDropState
{
    public int? DraggedCardId { get; set; }
    public int? SourceListId { get; set; }
    public int? DraggedListId { get; set; }
}
