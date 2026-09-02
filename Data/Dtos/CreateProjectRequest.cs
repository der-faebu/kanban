namespace Kanban.Data;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public int BoardId { get; set; }
}
