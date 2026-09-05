using Kanban.Data.Entities;

namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportCard
{
    public int Id { get; set; }
    // References another card's Id in this same export; null for a top-level card.
    public int? ParentCardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public CardPriority? Priority { get; set; }
    public CardState State { get; set; }
    public bool StateSetAutomatically { get; set; }
    public CardType? Type { get; set; }
    public string? DevReferenceUrl { get; set; }
    public string? TicketUrl { get; set; }
    public decimal? EstimatedHours { get; set; }
    public List<int> LabelIds { get; set; } = [];
    public List<int> ProjectIds { get; set; } = [];
    public List<string> AssigneeEmails { get; set; } = [];
    public List<BoardExportComment> Comments { get; set; } = [];
    public List<BoardExportChecklistItem> ChecklistItems { get; set; } = [];
    public List<BoardExportTimeLogEntry> TimeLogEntries { get; set; } = [];
    public List<BoardExportActivity> Activities { get; set; } = [];
    public List<BoardExportAttachment> Attachments { get; set; } = [];
}
