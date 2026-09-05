namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string UploadedByEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    // Path of this attachment's file entry within the zip's attachments/ folder.
    public string ZipEntryPath { get; set; } = string.Empty;
}
