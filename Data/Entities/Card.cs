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
    public CardState State { get; set; } = CardState.NotStarted;
    public bool StateSetAutomatically { get; set; }
    public CardType? Type { get; set; }
    public string? DevReferenceUrl { get; set; }
    public string? TicketUrl { get; set; }
    public decimal? EstimatedHours { get; set; }
    public int? ParentCardId { get; set; }
    public Card? ParentCard { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CardAssignee> Assignees { get; set; } = [];
    public ICollection<CardLabel> Labels { get; set; } = [];
    public ICollection<CardProject> Projects { get; set; } = [];
    public ICollection<Card> Children { get; set; } = [];
}
