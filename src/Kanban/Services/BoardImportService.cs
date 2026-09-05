using System.IO.Compression;
using System.Text.Json;
using Kanban.Data;
using Kanban.Data.Dtos;
using Kanban.Data.Dtos.BoardExport;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Services;

public interface IBoardImportService
{
    Task<BoardImportResult> ImportBoardAsync(string userId, Stream zipStream, string boardName);
}

public class BoardImportService(IDbContextFactory<ApplicationDbContext> contextFactory, IOptions<AttachmentsOptions> attachmentsOptions) : IBoardImportService
{
    private const int SupportedFormatVersion = 1;

    public async Task<BoardImportResult> ImportBoardAsync(string userId, Stream zipStream, string boardName)
    {
        // ZipArchive needs random access to read the central directory, but Blazor's
        // InputFile stream (what the browser upload form passes in) only supports
        // forward-only async reads. Buffer it to a seekable temp file first when needed.
        Stream? seekableCopy = null;
        if (!zipStream.CanSeek)
        {
            seekableCopy = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite,
                FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            await zipStream.CopyToAsync(seekableCopy);
            seekableCopy.Position = 0;
        }

        await using var _ = seekableCopy;
        var seekableStream = seekableCopy ?? zipStream;

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(seekableStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return new BoardImportResult { Success = false, Message = "The uploaded file is not a valid zip archive" };
        }

        using (archive)
        {
            var boardJsonEntry = archive.GetEntry("board.json");
            if (boardJsonEntry == null)
                return new BoardImportResult { Success = false, Message = "The uploaded zip is missing board.json" };

            byte[] boardJsonBytes;
            await using (var entryStream = boardJsonEntry.Open())
            using (var buffer = new MemoryStream())
            {
                await entryStream.CopyToAsync(buffer);
                boardJsonBytes = buffer.ToArray();
            }

            // Checked as a standalone step (rather than trusting BoardExportFile.FormatVersion's
            // C# default of 1) so a file that omits the field entirely is rejected as "missing",
            // not silently treated as a valid v1 export.
            int? formatVersion;
            try
            {
                using var probeDoc = JsonDocument.Parse(boardJsonBytes);
                formatVersion = probeDoc.RootElement.TryGetProperty("FormatVersion", out var fv) && fv.ValueKind == JsonValueKind.Number
                    ? fv.GetInt32()
                    : null;
            }
            catch (JsonException ex)
            {
                return new BoardImportResult { Success = false, Message = $"board.json is not valid JSON: {ex.Message}" };
            }

            if (formatVersion != SupportedFormatVersion)
            {
                return new BoardImportResult
                {
                    Success = false,
                    Message = formatVersion == null
                        ? "board.json is missing a format-version marker"
                        : $"Unsupported export format version {formatVersion}"
                };
            }

            BoardExportFile? export;
            try
            {
                export = JsonSerializer.Deserialize<BoardExportFile>(boardJsonBytes);
            }
            catch (JsonException ex)
            {
                return new BoardImportResult { Success = false, Message = $"board.json is not valid: {ex.Message}" };
            }

            if (export == null)
                return new BoardImportResult { Success = false, Message = "board.json is empty or invalid" };

            await using var context = await contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();

            // Attachment files land on disk as each card is processed, outside the DB
            // transaction's reach -- tracked here so a rollback can also erase them, keeping
            // "no partially-created board is left behind" true of the filesystem too.
            var writtenAttachmentPaths = new List<string>();

            try
            {
                var users = await context.Users.ToListAsync();
                var userByEmail = users
                    .Where(u => !string.IsNullOrEmpty(u.Email))
                    .ToDictionary(u => u.Email!, u => u, StringComparer.OrdinalIgnoreCase);

                // Falls back to the importing user when an export's email doesn't match any
                // account on this instance -- same fallback the Trello importer uses for
                // comment authorship.
                string ResolveUserId(string? email) =>
                    !string.IsNullOrEmpty(email) && userByEmail.TryGetValue(email, out var user) ? user.Id : userId;

                var board = new Board
                {
                    Name = boardName,
                    Description = export.BoardDescription,
                    OwnerId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Boards.Add(board);
                await context.SaveChangesAsync();

                var labelMap = new Dictionary<int, Label>();
                foreach (var exportLabel in export.Labels)
                {
                    var label = new Label { BoardId = board.Id, Name = exportLabel.Name, Color = exportLabel.Color };
                    context.Labels.Add(label);
                    labelMap[exportLabel.Id] = label;
                }
                await context.SaveChangesAsync();

                // Projects aren't board-scoped in this app, so a restore always creates its own
                // fresh set instead of guessing which existing project a name should merge into.
                var projectMap = new Dictionary<int, Project>();
                foreach (var exportProject in export.Projects)
                {
                    var project = new Project { Name = exportProject.Name, Color = exportProject.Color };
                    context.Projects.Add(project);
                    projectMap[exportProject.Id] = project;
                }
                await context.SaveChangesAsync();

                var listMap = new Dictionary<int, List>();
                foreach (var exportList in export.Lists.OrderBy(l => l.Position))
                {
                    var list = new List
                    {
                        BoardId = board.Id,
                        Name = exportList.Name,
                        Position = exportList.Position,
                        AssociatedState = exportList.AssociatedState,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Lists.Add(list);
                    listMap[exportList.Id] = list;
                }
                await context.SaveChangesAsync();

                var allExportCards = export.Lists
                    .SelectMany(list => list.Cards.Select(card => (List: list, Card: card)))
                    .ToList();

                // Pass 1: create every card before wiring up ParentCardId, so a sub-card can
                // resolve its parent regardless of which order the export lists them in.
                var cardMap = new Dictionary<int, Card>();
                foreach (var (exportList, exportCard) in allExportCards)
                {
                    var card = new Card
                    {
                        ListId = listMap[exportList.Id].Id,
                        Title = exportCard.Title,
                        Description = exportCard.Description,
                        Position = exportCard.Position,
                        DueDate = exportCard.DueDate,
                        Priority = exportCard.Priority,
                        State = exportCard.State,
                        StateSetAutomatically = exportCard.StateSetAutomatically,
                        Type = exportCard.Type,
                        DevReferenceUrl = exportCard.DevReferenceUrl,
                        TicketUrl = exportCard.TicketUrl,
                        EstimatedHours = exportCard.EstimatedHours,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    context.Cards.Add(card);
                    cardMap[exportCard.Id] = card;
                }
                await context.SaveChangesAsync();

                // Pass 2: now that every card has an id, remap ParentCardId from the export's
                // own ids to the newly created ones.
                foreach (var (_, exportCard) in allExportCards)
                {
                    if (exportCard.ParentCardId.HasValue && cardMap.TryGetValue(exportCard.ParentCardId.Value, out var parent))
                    {
                        cardMap[exportCard.Id].ParentCardId = parent.Id;
                    }
                }
                await context.SaveChangesAsync();

                foreach (var (_, exportCard) in allExportCards)
                {
                    var card = cardMap[exportCard.Id];

                    foreach (var labelId in exportCard.LabelIds)
                    {
                        if (labelMap.TryGetValue(labelId, out var label))
                            context.CardLabels.Add(new CardLabel { CardId = card.Id, LabelId = label.Id });
                    }

                    foreach (var projectId in exportCard.ProjectIds)
                    {
                        if (projectMap.TryGetValue(projectId, out var project))
                            context.CardProjects.Add(new CardProject { CardId = card.Id, ProjectId = project.Id });
                    }

                    foreach (var email in exportCard.AssigneeEmails)
                    {
                        if (userByEmail.TryGetValue(email, out var assignee))
                            context.CardAssignees.Add(new CardAssignee { CardId = card.Id, UserId = assignee.Id });
                    }

                    foreach (var comment in exportCard.Comments)
                    {
                        context.Comments.Add(new Comment
                        {
                            CardId = card.Id,
                            AuthorId = ResolveUserId(comment.AuthorEmail),
                            Text = comment.Text,
                            CreatedAt = comment.CreatedAt,
                            UpdatedAt = comment.UpdatedAt
                        });
                    }

                    foreach (var item in exportCard.ChecklistItems)
                    {
                        context.ChecklistItems.Add(new ChecklistItem
                        {
                            CardId = card.Id,
                            Text = item.Text,
                            IsDone = item.IsDone,
                            Position = item.Position
                        });
                    }

                    foreach (var entry in exportCard.TimeLogEntries)
                    {
                        context.TimeLogEntries.Add(new TimeLogEntry
                        {
                            CardId = card.Id,
                            UserId = ResolveUserId(entry.UserEmail),
                            Date = entry.Date,
                            DurationHours = entry.DurationHours,
                            Note = entry.Note
                        });
                    }

                    foreach (var activity in exportCard.Activities)
                    {
                        if (Enum.TryParse<ActivityType>(activity.ActivityType, out var activityType))
                        {
                            context.CardActivities.Add(new CardActivity
                            {
                                CardId = card.Id,
                                UserId = ResolveUserId(activity.UserEmail),
                                ActivityType = activityType,
                                Metadata = activity.Metadata,
                                CreatedAt = activity.CreatedAt
                            });
                        }
                    }

                    foreach (var attachment in exportCard.Attachments)
                    {
                        var zipEntry = archive.GetEntry(attachment.ZipEntryPath);
                        if (zipEntry == null)
                            continue;

                        var relativePath = Path.Combine(card.Id.ToString(), $"{Guid.NewGuid()}-{attachment.FileName}");
                        var absolutePath = Path.Combine(attachmentsOptions.Value.StorageRoot, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

                        await using (var zipEntryStream = zipEntry.Open())
                        await using (var destination = File.Create(absolutePath))
                        {
                            await zipEntryStream.CopyToAsync(destination);
                        }
                        writtenAttachmentPaths.Add(absolutePath);

                        context.Attachments.Add(new Attachment
                        {
                            CardId = card.Id,
                            UploadedByUserId = ResolveUserId(attachment.UploadedByEmail),
                            FileName = attachment.FileName,
                            ContentType = attachment.ContentType,
                            SizeBytes = attachment.SizeBytes,
                            StoragePath = relativePath,
                            CreatedAt = attachment.CreatedAt
                        });
                    }
                }
                await context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new BoardImportResult
                {
                    Success = true,
                    Message = $"Board '{boardName}' imported successfully",
                    BoardId = board.Id,
                    ListsImported = listMap.Count,
                    CardsImported = cardMap.Count,
                    LabelsCreated = labelMap.Count,
                    ProjectsCreated = projectMap.Count
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                foreach (var path in writtenAttachmentPaths)
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }

                return new BoardImportResult { Success = false, Message = $"Import failed: {ex.Message}" };
            }
        }
    }
}
