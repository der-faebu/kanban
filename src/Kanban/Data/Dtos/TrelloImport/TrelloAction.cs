namespace Kanban.Data.Dtos.TrelloImport;

public class TrelloAction
{
    public string? Id { get; set; }
    public string? Type { get; set; }
    public DateTime? Date { get; set; }
    public TrelloActionData? Data { get; set; }
    public TrelloMember? MemberCreator { get; set; }
}

public class TrelloActionData
{
    public string? Text { get; set; }
    public TrelloActionCard? Card { get; set; }
}

public class TrelloActionCard
{
    public string? Id { get; set; }
}
