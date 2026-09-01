using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ICommentService
{
    Task<Comment> AddCommentAsync(int cardId, string userId, string text);
    Task<List<Comment>> GetCardCommentsAsync(int cardId, string userId);
    Task<int> GetCardCommentCountAsync(int cardId, string userId);
    Task UpdateCommentAsync(int commentId, string userId, string text);
    Task DeleteCommentAsync(int commentId, string userId);
}

public class CommentService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardService boardService, IBoardSyncService boardSyncService, IActivityLogService activityLogService) : ICommentService
{
    public async Task<Comment> AddCommentAsync(int cardId, string userId, string text)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var now = DateTime.UtcNow;
        var comment = new Comment
        {
            CardId = cardId,
            AuthorId = userId,
            Text = text,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Comments.Add(comment);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCommentAddedAsync(card.List.BoardId, cardId, comment.Id, userId, comment.Text, comment.CreatedAt);

        var metadata = new { commentId = comment.Id };
        await activityLogService.LogAsync(cardId, userId, ActivityType.CommentAdded, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(card.List.BoardId, cardId, ActivityType.CommentAdded, userId, metadata, DateTime.UtcNow);

        return comment;
    }

    public async Task<List<Comment>> GetCardCommentsAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Comments
            .Where(c => c.CardId == cardId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetCardCommentCountAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Comments.CountAsync(c => c.CardId == cardId && !c.IsDeleted);
    }

    public async Task UpdateCommentAsync(int commentId, string userId, string text)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.Include(c => c.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
        if (comment?.Card?.List == null)
            throw new InvalidOperationException("Comment not found");

        var isMember = await listService.IsUserBoardMemberAsync(comment.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        if (comment.AuthorId != userId)
            throw new InvalidOperationException("Only the comment author can edit this comment");

        comment.Text = text;
        comment.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCommentUpdatedAsync(comment.Card.List.BoardId, comment.CardId, commentId, comment.Text, comment.UpdatedAt);

        var metadata = new { commentId };
        await activityLogService.LogAsync(comment.CardId, userId, ActivityType.CommentUpdated, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(comment.Card.List.BoardId, comment.CardId, ActivityType.CommentUpdated, userId, metadata, DateTime.UtcNow);
    }

    public async Task DeleteCommentAsync(int commentId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var comment = await context.Comments.Include(c => c.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted);
        if (comment?.Card?.List == null)
            throw new InvalidOperationException("Comment not found");

        var boardId = comment.Card.List.BoardId;
        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var isOwner = await boardService.IsUserBoardOwnerAsync(boardId, userId);
        if (comment.AuthorId != userId && !isOwner)
            throw new InvalidOperationException("Only the comment author or board owner can delete this comment");

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCommentDeletedAsync(boardId, comment.CardId, commentId);

        var metadata = new { commentId };
        await activityLogService.LogAsync(comment.CardId, userId, ActivityType.CommentDeleted, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, comment.CardId, ActivityType.CommentDeleted, userId, metadata, DateTime.UtcNow);
    }
}
