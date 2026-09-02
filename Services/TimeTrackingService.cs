using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ITimeTrackingService
{
    Task<TimeLogEntry> AddTimeLogEntryAsync(int cardId, string userId, DateTime date, decimal durationHours, string? note);
    Task<List<TimeLogEntry>> GetCardTimeLogEntriesAsync(int cardId, string userId);
    Task<decimal> GetCardLoggedHoursAsync(int cardId, string userId);
    Task UpdateTimeLogEntryAsync(int entryId, string userId, DateTime date, decimal durationHours, string? note);
    Task DeleteTimeLogEntryAsync(int entryId, string userId);
}

public class TimeTrackingService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardService boardService, IBoardSyncService boardSyncService) : ITimeTrackingService
{
    public async Task<TimeLogEntry> AddTimeLogEntryAsync(int cardId, string userId, DateTime date, decimal durationHours, string? note)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var now = DateTime.UtcNow;
        var entry = new TimeLogEntry
        {
            CardId = cardId,
            UserId = userId,
            Date = date,
            DurationHours = durationHours,
            Note = note,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.TimeLogEntries.Add(entry);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastTimeLogEntryAddedAsync(card.List.BoardId, cardId, entry.Id);

        return entry;
    }

    public async Task<List<TimeLogEntry>> GetCardTimeLogEntriesAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.TimeLogEntries
            .Where(t => t.CardId == cardId && !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .ToListAsync();
    }

    public async Task<decimal> GetCardLoggedHoursAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.TimeLogEntries
            .Where(t => t.CardId == cardId && !t.IsDeleted)
            .SumAsync(t => (decimal?)t.DurationHours) ?? 0m;
    }

    public async Task UpdateTimeLogEntryAsync(int entryId, string userId, DateTime date, decimal durationHours, string? note)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var entry = await context.TimeLogEntries.Include(t => t.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(t => t.Id == entryId && !t.IsDeleted);
        if (entry?.Card?.List == null)
            throw new InvalidOperationException("Time log entry not found");

        var isMember = await listService.IsUserBoardMemberAsync(entry.Card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        if (entry.UserId != userId)
            throw new InvalidOperationException("Only the entry author can edit this time log entry");

        entry.Date = date;
        entry.DurationHours = durationHours;
        entry.Note = note;
        entry.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastTimeLogEntryUpdatedAsync(entry.Card.List.BoardId, entry.CardId, entryId);
    }

    public async Task DeleteTimeLogEntryAsync(int entryId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var entry = await context.TimeLogEntries.Include(t => t.Card).ThenInclude(c => c!.List)
            .FirstOrDefaultAsync(t => t.Id == entryId && !t.IsDeleted);
        if (entry?.Card?.List == null)
            throw new InvalidOperationException("Time log entry not found");

        var boardId = entry.Card.List.BoardId;
        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var isOwner = await boardService.IsUserBoardOwnerAsync(boardId, userId);
        if (entry.UserId != userId && !isOwner)
            throw new InvalidOperationException("Only the entry author or board owner can delete this time log entry");

        entry.IsDeleted = true;
        entry.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastTimeLogEntryDeletedAsync(boardId, entry.CardId, entryId);
    }
}
