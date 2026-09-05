namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportChecklistItem
{
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int Position { get; set; }
}
