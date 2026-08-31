namespace Kanban.Data;

public class UpdateCardRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
