namespace Kanban.Data.Dtos;

public class BoardImportResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? BoardId { get; set; }
    public int ListsImported { get; set; }
    public int CardsImported { get; set; }
    public int LabelsCreated { get; set; }
    public int ProjectsCreated { get; set; }
}
