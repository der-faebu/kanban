namespace Kanban.Data.Entities;

public class List
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public CardState? AssociatedState { get; set; }
    public ICollection<Card> Cards { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
