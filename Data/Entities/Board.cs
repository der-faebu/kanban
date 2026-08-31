namespace Kanban.Data.Entities;

public class Board
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    public ICollection<BoardMember> Members { get; set; } = [];
    public ICollection<List> Lists { get; set; } = [];
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
