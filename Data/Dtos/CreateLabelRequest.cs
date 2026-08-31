namespace Kanban.Data;

public class CreateLabelRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
}
