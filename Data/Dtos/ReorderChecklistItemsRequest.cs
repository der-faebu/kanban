namespace Kanban.Data;

// A plain class (not a ValueTuple) because System.Text.Json only serializes public
// properties, not the public fields ValueTuple exposes — binding this straight to
// List<(int ItemId, int Position)> would silently deserialize every entry as (0, 0).
public class ChecklistItemPosition
{
    public int ItemId { get; set; }
    public int Position { get; set; }
}

public class ReorderChecklistItemsRequest
{
    public List<ChecklistItemPosition> Positions { get; set; } = [];
}
