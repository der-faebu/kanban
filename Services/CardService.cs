using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ICardService
{
    Task<Card> CreateCardAsync(int listId, string userId, string title, string description);
    Task<Card?> GetCardByIdAsync(int cardId, string userId);
    Task<List<Card>> GetListCardsAsync(int listId, string userId);
    Task UpdateCardAsync(int cardId, string userId, string title, string description);
    Task MoveCardAsync(int cardId, string userId, int targetListId, int position);
    Task ReorderCardsAsync(int listId, string userId, List<(int CardId, int Position)> positions);
    Task SoftDeleteCardAsync(int cardId, string userId);
    Task RestoreCardAsync(int cardId, string userId);
}

public class CardService(ApplicationDbContext context, IListService listService) : ICardService
{
    public async Task<Card> CreateCardAsync(int listId, string userId, string title, string description)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);
        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await listService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var maxPosition = await context.Cards
            .Where(c => c.ListId == listId && !c.IsDeleted)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var card = new Card
        {
            ListId = listId,
            Title = title,
            Description = description,
            Position = maxPosition + 1,
            IsDeleted = false
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();
        return card;
    }

    public async Task<Card?> GetCardByIdAsync(int cardId, string userId)
    {
        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            return null;

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        return isMember ? card : null;
    }

    public async Task<List<Card>> GetListCardsAsync(int listId, string userId)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);
        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await listService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Cards
            .Where(c => c.ListId == listId && !c.IsDeleted)
            .OrderBy(c => c.Position)
            .ToListAsync();
    }

    public async Task UpdateCardAsync(int cardId, string userId, string title, string description)
    {
        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        card.Title = title;
        card.Description = description;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task MoveCardAsync(int cardId, string userId, int targetListId, int position)
    {
        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var targetList = await context.Lists.FirstOrDefaultAsync(l => l.Id == targetListId && !l.IsDeleted);
        if (targetList == null)
            throw new InvalidOperationException("Target list not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        card.ListId = targetListId;
        card.Position = position;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task ReorderCardsAsync(int listId, string userId, List<(int CardId, int Position)> positions)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);
        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await listService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var cards = await context.Cards
            .Where(c => c.ListId == listId && !c.IsDeleted)
            .ToListAsync();

        foreach (var (cardId, position) in positions)
        {
            var card = cards.FirstOrDefault(c => c.Id == cardId);
            if (card != null)
            {
                card.Position = position;
                card.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SoftDeleteCardAsync(int cardId, string userId)
    {
        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        card.IsDeleted = true;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task RestoreCardAsync(int cardId, string userId)
    {
        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        card.IsDeleted = false;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
