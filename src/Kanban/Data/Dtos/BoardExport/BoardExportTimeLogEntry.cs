namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportTimeLogEntry
{
    public string UserEmail { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal DurationHours { get; set; }
    public string? Note { get; set; }
}
