namespace Kanban.Data.Entities;

public class Attachment
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card? Card { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public ApplicationUser? UploadedBy { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
