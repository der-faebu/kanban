namespace Kanban.Data.Dtos.BoardExport;

public class BoardExportFile
{
    // Bumped whenever the shape below changes in a way that would break a naive importer,
    // so a future import endpoint can detect and reject an incompatible export up front.
    public int FormatVersion { get; set; } = 1;
    public string BoardName { get; set; } = string.Empty;
    public string BoardDescription { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; }
    public List<BoardExportLabel> Labels { get; set; } = [];
    public List<BoardExportProject> Projects { get; set; } = [];
    public List<BoardExportList> Lists { get; set; } = [];
}
