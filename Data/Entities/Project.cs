namespace Kanban.Data.Entities;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CardProject> Cards { get; set; } = [];
}
