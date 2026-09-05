namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportComment
{
    public string AuthorEmail { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
