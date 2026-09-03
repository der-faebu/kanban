namespace Kanban.Data.Dtos.TrelloImport;

public class TrelloChecklist
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? IdCard { get; set; }
    public int Pos { get; set; }
    public List<TrelloCheckItem> CheckItems { get; set; } = [];
}

public class TrelloCheckItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? State { get; set; }
    public int Pos { get; set; }
}
