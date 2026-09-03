using Kanban.Data.Entities;

namespace Kanban.Data;

public class AddBoardMemberRequest
{
    public string MemberId { get; set; } = string.Empty;
    public BoardMemberRole Role { get; set; }
}
