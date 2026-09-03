namespace Kanban.Data;

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";

    // The board the card was opened from, if creation was triggered from a card's detail
    // view -- gates creation on membership of that board. Omit (null) for a board-less
    // creation (e.g. the standalone Projects page), which only requires authentication.
    public int? BoardId { get; set; }
}
