namespace Kanban.Data.Entities;

public class ChecklistItem
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
