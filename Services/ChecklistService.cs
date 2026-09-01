using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IChecklistService
{
    Task<ChecklistItem> AddItemAsync(int cardId, string userId, string text);
    Task<List<ChecklistItem>> GetItemsAsync(int cardId, string userId);
    Task<(int Done, int Total)> GetChecklistProgressAsync(int cardId, string userId);
    Task UpdateItemTextAsync(int itemId, string userId, string text);
    Task ToggleItemAsync(int itemId, string userId, bool isDone);
    Task DeleteItemAsync(int itemId, string userId);
    Task ReorderItemsAsync(int cardId, string userId, List<(int ItemId, int Position)> positions);
}

public class ChecklistService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardSyncService boardSyncService, IActivityLogService activityLogService) : IChecklistService
{
    public async Task<ChecklistItem> AddItemAsync(int cardId, string userId, string text)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var maxPosition = await context.ChecklistItems
            .Where(i => i.CardId == cardId)
            .MaxAsync(i => (int?)i.Position) ?? -1;

        var item = new ChecklistItem
        {
            CardId = cardId,
            Text = text,
            IsDone = false,
            Position = maxPosition + 1
        };

        context.ChecklistItems.Add(item);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastChecklistItemAddedAsync(card.List.BoardId, cardId, item.Id, item.Text, item.Position);

        var metadata = new { itemId = item.Id, text = item.Text };
        await activityLogService.LogAsync(cardId, userId, ActivityType.ChecklistItemAdded, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(card.List.BoardId, cardId, ActivityType.ChecklistItemAdded, userId, metadata, DateTime.UtcNow);

        return item;
    }

    public async Task<List<ChecklistItem>> GetItemsAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.ChecklistItems
            .Where(i => i.CardId == cardId)
            .OrderBy(i => i.Position)
            .ToListAsync();
    }

    public async Task<(int Done, int Total)> GetChecklistProgressAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var total = await context.ChecklistItems.CountAsync(i => i.CardId == cardId);
        var done = await context.ChecklistItems.CountAsync(i => i.CardId == cardId && i.IsDone);
        return (done, total);
    }

    public async Task UpdateItemTextAsync(int itemId, string userId, string text)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var item = await context.ChecklistItems.Include(i => i.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item?.Card?.List == null)
            throw new InvalidOperationException("Checklist item not found");

        var isMember = await listService.IsUserBoardMemberAsync(item.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        item.Text = text;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastChecklistItemUpdatedAsync(item.Card.List.BoardId, item.CardId, itemId, item.Text, item.IsDone);
    }

    public async Task ToggleItemAsync(int itemId, string userId, bool isDone)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var item = await context.ChecklistItems.Include(i => i.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item?.Card?.List == null)
            throw new InvalidOperationException("Checklist item not found");

        var isMember = await listService.IsUserBoardMemberAsync(item.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        item.IsDone = isDone;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastChecklistItemUpdatedAsync(item.Card.List.BoardId, item.CardId, itemId, item.Text, item.IsDone);

        var metadata = new { itemId, text = item.Text, isDone = item.IsDone };
        await activityLogService.LogAsync(item.CardId, userId, ActivityType.ChecklistItemToggled, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(item.Card.List.BoardId, item.CardId, ActivityType.ChecklistItemToggled, userId, metadata, DateTime.UtcNow);
    }

    public async Task DeleteItemAsync(int itemId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var item = await context.ChecklistItems.Include(i => i.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(i => i.Id == itemId);
        if (item?.Card?.List == null)
            throw new InvalidOperationException("Checklist item not found");

        var isMember = await listService.IsUserBoardMemberAsync(item.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = item.Card.List.BoardId;
        var cardId = item.CardId;
        var itemText = item.Text;
        context.ChecklistItems.Remove(item);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastChecklistItemDeletedAsync(boardId, cardId, itemId);

        var metadata = new { itemId, text = itemText };
        await activityLogService.LogAsync(cardId, userId, ActivityType.ChecklistItemDeleted, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.ChecklistItemDeleted, userId, metadata, DateTime.UtcNow);
    }

    public async Task ReorderItemsAsync(int cardId, string userId, List<(int ItemId, int Position)> positions)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var items = await context.ChecklistItems
            .Where(i => i.CardId == cardId)
            .ToListAsync();

        foreach (var (itemId, position) in positions)
        {
            var item = items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                item.Position = position;
                item.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        await boardSyncService.BroadcastChecklistReorderedAsync(card.List.BoardId, cardId, positions);
    }
}
