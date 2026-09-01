using System.Text.Json;
using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IActivityLogService
{
    Task LogAsync(int cardId, string userId, ActivityType type, object metadata);
    Task<List<CardActivity>> GetCardActivityAsync(int cardId, string userId);
}

public class ActivityLogService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService) : IActivityLogService
{
    public async Task LogAsync(int cardId, string userId, ActivityType type, object metadata)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var activity = new CardActivity
        {
            CardId = cardId,
            UserId = userId,
            ActivityType = type,
            Metadata = JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow
        };

        context.CardActivities.Add(activity);
        await context.SaveChangesAsync();
    }

    public async Task<List<CardActivity>> GetCardActivityAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.CardActivities
            .Where(a => a.CardId == cardId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }
}
