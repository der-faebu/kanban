namespace Kanban.Data.Entities;

public class CardAssignee
{
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
