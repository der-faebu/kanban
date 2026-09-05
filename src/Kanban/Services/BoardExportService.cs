using System.IO.Compression;
using System.Text.Json;
using Kanban.Data;
using Kanban.Data.Dtos.BoardExport;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Services;

public interface IBoardExportService
{
    Task<(Stream Zip, string BoardName)> ExportBoardAsync(int boardId, string userId);
}

public class BoardExportService(IDbContextFactory<ApplicationDbContext> contextFactory, IOptions<AttachmentsOptions> attachmentsOptions) : IBoardExportService
{
    public async Task<(Stream Zip, string BoardName)> ExportBoardAsync(int boardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);
        if (board == null)
            throw new InvalidOperationException("Board not found");

        // Only the owner can export -- matches the existing Trello import's ownership restriction.
        if (board.OwnerId != userId)
            throw new InvalidOperationException("Only the board owner can export this board.");

        var lists = await context.Lists
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .OrderBy(l => l.Position)
            .ToListAsync();
        var listIds = lists.Select(l => l.Id).ToHashSet();

        var cards = await context.Cards
            .Where(c => listIds.Contains(c.ListId) && !c.IsDeleted)
            .OrderBy(c => c.Position)
            .ToListAsync();
        var cardIds = cards.Select(c => c.Id).ToHashSet();

        var labels = await context.Labels
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .ToListAsync();

        var cardLabels = await context.CardLabels
            .Where(cl => cardIds.Contains(cl.CardId))
            .ToListAsync();

        var cardProjects = await context.CardProjects
            .Where(cp => cardIds.Contains(cp.CardId))
            .ToListAsync();
        var projectIds = cardProjects.Select(cp => cp.ProjectId).ToHashSet();
        var projects = await context.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToListAsync();

        var assignees = await context.CardAssignees
            .Where(ca => cardIds.Contains(ca.CardId))
            .Include(ca => ca.User)
            .ToListAsync();

        var comments = await context.Comments
            .Where(c => cardIds.Contains(c.CardId) && !c.IsDeleted)
            .Include(c => c.Author)
            .ToListAsync();

        var checklistItems = await context.ChecklistItems
            .Where(i => cardIds.Contains(i.CardId))
            .OrderBy(i => i.Position)
            .ToListAsync();

        var timeLogEntries = await context.TimeLogEntries
            .Where(t => cardIds.Contains(t.CardId) && !t.IsDeleted)
            .Include(t => t.User)
            .ToListAsync();

        var activities = await context.CardActivities
            .Where(a => cardIds.Contains(a.CardId))
            .Include(a => a.User)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        var attachments = await context.Attachments
            .Where(a => cardIds.Contains(a.CardId))
            .Include(a => a.UploadedBy)
            .ToListAsync();

        var exportFile = new BoardExportFile
        {
            BoardName = board.Name,
            BoardDescription = board.Description,
            ExportedAt = DateTime.UtcNow,
            Labels = labels.Select(l => new BoardExportLabel { Id = l.Id, Name = l.Name, Color = l.Color }).ToList(),
            Projects = projects.Select(p => new BoardExportProject { Id = p.Id, Name = p.Name, Color = p.Color }).ToList(),
            Lists = lists.Select(list => new BoardExportList
            {
                Id = list.Id,
                Name = list.Name,
                Position = list.Position,
                AssociatedState = list.AssociatedState,
                Cards = cards.Where(c => c.ListId == list.Id).Select(card => new BoardExportCard
                {
                    Id = card.Id,
                    ParentCardId = card.ParentCardId,
                    Title = card.Title,
                    Description = card.Description,
                    Position = card.Position,
                    DueDate = card.DueDate,
                    Priority = card.Priority,
                    State = card.State,
                    StateSetAutomatically = card.StateSetAutomatically,
                    Type = card.Type,
                    DevReferenceUrl = card.DevReferenceUrl,
                    TicketUrl = card.TicketUrl,
                    EstimatedHours = card.EstimatedHours,
                    LabelIds = cardLabels.Where(cl => cl.CardId == card.Id).Select(cl => cl.LabelId).ToList(),
                    ProjectIds = cardProjects.Where(cp => cp.CardId == card.Id).Select(cp => cp.ProjectId).ToList(),
                    AssigneeEmails = assignees.Where(ca => ca.CardId == card.Id).Select(ca => ca.User!.Email ?? "").ToList(),
                    Comments = comments.Where(c => c.CardId == card.Id).Select(c => new BoardExportComment
                    {
                        AuthorEmail = c.Author!.Email ?? "",
                        Text = c.Text,
                        CreatedAt = c.CreatedAt,
                        UpdatedAt = c.UpdatedAt
                    }).ToList(),
                    ChecklistItems = checklistItems.Where(i => i.CardId == card.Id).Select(i => new BoardExportChecklistItem
                    {
                        Text = i.Text,
                        IsDone = i.IsDone,
                        Position = i.Position
                    }).ToList(),
                    TimeLogEntries = timeLogEntries.Where(t => t.CardId == card.Id).Select(t => new BoardExportTimeLogEntry
                    {
                        UserEmail = t.User!.Email ?? "",
                        Date = t.Date,
                        DurationHours = t.DurationHours,
                        Note = t.Note
                    }).ToList(),
                    Activities = activities.Where(a => a.CardId == card.Id).Select(a => new BoardExportActivity
                    {
                        UserEmail = a.User!.Email ?? "",
                        ActivityType = a.ActivityType.ToString(),
                        Metadata = a.Metadata,
                        CreatedAt = a.CreatedAt
                    }).ToList(),
                    Attachments = attachments.Where(a => a.CardId == card.Id).Select(a => new BoardExportAttachment
                    {
                        FileName = a.FileName,
                        ContentType = a.ContentType,
                        SizeBytes = a.SizeBytes,
                        UploadedByEmail = a.UploadedBy!.Email ?? "",
                        CreatedAt = a.CreatedAt,
                        ZipEntryPath = $"attachments/{a.Id}/{a.FileName}"
                    }).ToList()
                }).ToList()
            }).ToList()
        };

        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var boardJsonEntry = archive.CreateEntry("board.json", CompressionLevel.Optimal);
            await using (var entryStream = boardJsonEntry.Open())
            {
                await JsonSerializer.SerializeAsync(entryStream, exportFile, new JsonSerializerOptions { WriteIndented = true });
            }

            foreach (var attachment in attachments)
            {
                var absolutePath = Path.Combine(attachmentsOptions.Value.StorageRoot, attachment.StoragePath);
                if (!File.Exists(absolutePath))
                    continue;

                var entry = archive.CreateEntry($"attachments/{attachment.Id}/{attachment.FileName}", CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(absolutePath);
                await fileStream.CopyToAsync(entryStream);
            }
        }

        zipStream.Position = 0;
        return (zipStream, board.Name);
    }
}
