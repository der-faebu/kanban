namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportActivity
{
    public string UserEmail { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
