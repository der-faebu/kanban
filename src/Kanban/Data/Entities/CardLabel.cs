namespace Kanban.Data.Entities;

public class CardLabel
{
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public int LabelId { get; set; }
    public Label? Label { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
