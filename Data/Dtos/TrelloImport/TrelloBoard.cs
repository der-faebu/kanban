namespace Kanban.Data.Dtos.TrelloImport;

public class TrelloBoard
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Desc { get; set; }
    public List<TrelloList> Lists { get; set; } = [];
    public List<TrelloCard> Cards { get; set; } = [];
    public List<TrelloLabel> Labels { get; set; } = [];
    public List<TrelloMember> Members { get; set; } = [];
}
