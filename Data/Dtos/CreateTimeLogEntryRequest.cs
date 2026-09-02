namespace Kanban.Data;

public class CreateTimeLogEntryRequest
{
    public DateTime Date { get; set; }
    public decimal DurationHours { get; set; }
    public string? Note { get; set; }
}
