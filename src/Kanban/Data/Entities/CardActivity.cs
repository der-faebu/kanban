namespace Kanban.Data.Entities;

public class CardActivity
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public ActivityType ActivityType { get; set; }
    public string Metadata { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
