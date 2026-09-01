namespace Kanban.Data.Entities;

public class Card
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public List? List { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public CardPriority? Priority { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CardAssignee> Assignees { get; set; } = [];
    public ICollection<CardLabel> Labels { get; set; } = [];
}
