namespace Kanban.Data.Dtos.TrelloImport;

public class TrelloCard
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public string? IdList { get; set; }
    public int Pos { get; set; }
    public DateTime? Due { get; set; }
    public List<string> IdMembers { get; set; } = [];
    public List<string> IdLabels { get; set; } = [];
}
