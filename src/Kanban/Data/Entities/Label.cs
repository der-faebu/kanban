namespace Kanban.Data.Entities;

public class Label
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CardLabel> Cards { get; set; } = [];
}
