namespace Kanban.Data;

public class CreateCardRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
