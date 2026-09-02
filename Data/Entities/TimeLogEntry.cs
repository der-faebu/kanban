namespace Kanban.Data.Entities;

public class TimeLogEntry
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public DateTime Date { get; set; }
    public decimal DurationHours { get; set; }
    public string? Note { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
