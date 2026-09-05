using Kanban.Data.Entities;

namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportList
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public CardState? AssociatedState { get; set; }
    public List<BoardExportCard> Cards { get; set; } = [];
}
