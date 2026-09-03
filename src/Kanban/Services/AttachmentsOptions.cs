namespace Kanban.Services;

public class AttachmentsOptions
{
    public string StorageRoot { get; set; } = "uploads";
    public int MaxFileSizeMb { get; set; } = 25;
}
