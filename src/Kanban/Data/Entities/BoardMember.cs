namespace Kanban.Data.Entities;

public class BoardMember
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board? Board { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public BoardMemberRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
