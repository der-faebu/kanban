namespace Kanban.Data.Entities;

public class CardProject
{
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
