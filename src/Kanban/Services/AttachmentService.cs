using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kanban.Services;

public interface IAttachmentService
{
    Task<Attachment> UploadAttachmentAsync(int cardId, string userId, Stream fileStream, string fileName, string contentType, long sizeBytes);
    Task<List<Attachment>> GetAttachmentsAsync(int cardId, string userId);
    Task<int> GetAttachmentCountAsync(int cardId, string userId);
    Task<(Stream Stream, string FileName, string ContentType)> GetAttachmentStreamAsync(int attachmentId, string userId);
    Task DeleteAttachmentAsync(int attachmentId, string userId);
}

public class AttachmentService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardSyncService boardSyncService, IOptions<AttachmentsOptions> options) : IAttachmentService
{
    public async Task<Attachment> UploadAttachmentAsync(int cardId, string userId, Stream fileStream, string fileName, string contentType, long sizeBytes)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var maxBytes = options.Value.MaxFileSizeMb * 1024L * 1024L;
        if (sizeBytes > maxBytes)
            throw new InvalidOperationException($"File exceeds the maximum size of {options.Value.MaxFileSizeMb} MB");

        var relativePath = Path.Combine(cardId.ToString(), $"{Guid.NewGuid()}-{fileName}");
        var absolutePath = Path.Combine(options.Value.StorageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        try
        {
            await using (var destination = File.Create(absolutePath))
            {
                await fileStream.CopyToAsync(destination);
            }
        }
        catch
        {
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
            throw;
        }

        var attachment = new Attachment
        {
            CardId = cardId,
            UploadedByUserId = userId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StoragePath = relativePath,
            CreatedAt = DateTime.UtcNow
        };

        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastAttachmentAddedAsync(card.List.BoardId, cardId, attachment.Id, fileName, userId);
        return attachment;
    }

    public async Task<List<Attachment>> GetAttachmentsAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Attachments
            .Where(a => a.CardId == cardId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetAttachmentCountAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Attachments.CountAsync(a => a.CardId == cardId);
    }

    public async Task<(Stream Stream, string FileName, string ContentType)> GetAttachmentStreamAsync(int attachmentId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var attachment = await context.Attachments.Include(a => a.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (attachment?.Card?.List == null)
            throw new InvalidOperationException("Attachment not found");

        var isMember = await listService.IsUserBoardMemberAsync(attachment.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var absolutePath = Path.Combine(options.Value.StorageRoot, attachment.StoragePath);
        var stream = File.OpenRead(absolutePath);
        return (stream, attachment.FileName, attachment.ContentType);
    }

    public async Task DeleteAttachmentAsync(int attachmentId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var attachment = await context.Attachments.Include(a => a.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(a => a.Id == attachmentId);
        if (attachment?.Card?.List == null)
            throw new InvalidOperationException("Attachment not found");

        var boardId = attachment.Card.List.BoardId;
        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var cardId = attachment.CardId;
        var absolutePath = Path.Combine(options.Value.StorageRoot, attachment.StoragePath);

        context.Attachments.Remove(attachment);
        await context.SaveChangesAsync();

        if (File.Exists(absolutePath))
            File.Delete(absolutePath);

        await boardSyncService.BroadcastAttachmentDeletedAsync(boardId, cardId, attachmentId);
    }
}
