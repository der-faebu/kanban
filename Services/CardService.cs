using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ICardService
{
    Task<Card> CreateCardAsync(int listId, string userId, string title, string description);
    Task<Card?> GetCardByIdAsync(int cardId, string userId);
    Task<List<Card>> GetListCardsAsync(int listId, string userId);
    Task UpdateCardAsync(int cardId, string userId, string title, string description, DateTime? dueDate);
    Task SetCardPriorityAsync(int cardId, string userId, CardPriority? priority);
    Task SetCardStateAsync(int cardId, string userId, CardState state);
    Task SetCardTypeAsync(int cardId, string userId, CardType? type);
    Task SetDevReferenceUrlAsync(int cardId, string userId, string? url);
    Task SetTicketUrlAsync(int cardId, string userId, string? url);
    Task SetEstimatedHoursAsync(int cardId, string userId, decimal? estimatedHours);
    Task<Card> CreateSubCardAsync(int parentCardId, string userId, string title, string description);
    Task<List<Card>> GetSubCardsAsync(int parentCardId, string userId);
    Task MoveCardAsync(int cardId, string userId, int targetListId, int position);
    Task ReorderCardsAsync(int listId, string userId, List<(int CardId, int Position)> positions);
    Task SoftDeleteCardAsync(int cardId, string userId);
    Task RestoreCardAsync(int cardId, string userId);
    Task AddAssigneeAsync(int cardId, string userId, string assigneeUserId);
    Task RemoveAssigneeAsync(int cardId, string userId, string assigneeUserId);
    Task<List<ApplicationUser>> GetCardAssigneesAsync(int cardId, string userId);
    Task AddLabelAsync(int cardId, string userId, int labelId);
    Task RemoveLabelAsync(int cardId, string userId, int labelId);
    Task<List<Label>> GetCardLabelsAsync(int cardId, string userId);
    Task AddProjectAsync(int cardId, string userId, int projectId);
    Task RemoveProjectAsync(int cardId, string userId, int projectId);
    Task<List<Project>> GetCardProjectsAsync(int cardId, string userId);
}

public class CardService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardSyncService boardSyncService, IActivityLogService activityLogService) : ICardService
{
    public async Task<Card> CreateCardAsync(int listId, string userId, string title, string description)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

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
        await boardSyncService.BroadcastCardCreatedAsync(list.BoardId, card.Id, card.Title, card.ListId);

        var metadata = new { title = card.Title, listId = card.ListId };
        await activityLogService.LogAsync(card.Id, userId, ActivityType.CardCreated, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(list.BoardId, card.Id, ActivityType.CardCreated, userId, metadata, DateTime.UtcNow);

        return card;
    }

    public async Task<Card?> GetCardByIdAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            return null;

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        return isMember ? card : null;
    }

    public async Task<List<Card>> GetListCardsAsync(int listId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

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

    public async Task UpdateCardAsync(int cardId, string userId, string title, string description, DateTime? dueDate)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.Title = title;
        card.Description = description;
        card.DueDate = dueDate;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardUpdatedAsync(boardId, cardId, title, description, dueDate);

        var metadata = new { title, description, dueDate };
        await activityLogService.LogAsync(cardId, userId, ActivityType.CardUpdated, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.CardUpdated, userId, metadata, DateTime.UtcNow);
    }

    public async Task SetCardPriorityAsync(int cardId, string userId, CardPriority? priority)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.Priority = priority;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardPriorityChangedAsync(boardId, cardId, priority);

        var metadata = new { priority = priority?.ToString() };
        await activityLogService.LogAsync(cardId, userId, ActivityType.PriorityChanged, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.PriorityChanged, userId, metadata, DateTime.UtcNow);
    }

    public async Task SetCardStateAsync(int cardId, string userId, CardState state)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.State = state;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardStateChangedAsync(boardId, cardId, state);

        var metadata = new { state = state.ToString() };
        await activityLogService.LogAsync(cardId, userId, ActivityType.StateChanged, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.StateChanged, userId, metadata, DateTime.UtcNow);
    }

    public async Task SetCardTypeAsync(int cardId, string userId, CardType? type)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.Type = type;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardTypeChangedAsync(boardId, cardId, type);
    }

    public async Task SetDevReferenceUrlAsync(int cardId, string userId, string? url)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.DevReferenceUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardDevReferenceUrlChangedAsync(boardId, cardId, card.DevReferenceUrl);
    }

    public async Task SetTicketUrlAsync(int cardId, string userId, string? url)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.TicketUrl = string.IsNullOrWhiteSpace(url) ? null : url;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardTicketUrlChangedAsync(boardId, cardId, card.TicketUrl);
    }

    public async Task SetEstimatedHoursAsync(int cardId, string userId, decimal? estimatedHours)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.EstimatedHours = estimatedHours;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardEstimatedHoursChangedAsync(boardId, cardId, estimatedHours);
    }

    public async Task<Card> CreateSubCardAsync(int parentCardId, string userId, string title, string description)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var parent = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == parentCardId && !c.IsDeleted);
        if (parent?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(parent.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        if (parent.ParentCardId.HasValue)
            throw new InvalidOperationException("Cannot add a sub-card to a card that is itself a sub-card");

        var maxPosition = await context.Cards
            .Where(c => c.ListId == parent.ListId && !c.IsDeleted)
            .MaxAsync(c => (int?)c.Position) ?? -1;

        var card = new Card
        {
            ListId = parent.ListId,
            ParentCardId = parentCardId,
            Title = title,
            Description = description,
            Position = maxPosition + 1,
            IsDeleted = false
        };

        context.Cards.Add(card);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardCreatedAsync(parent.List.BoardId, card.Id, card.Title, card.ListId);

        var metadata = new { title = card.Title, listId = card.ListId, parentCardId };
        await activityLogService.LogAsync(card.Id, userId, ActivityType.CardCreated, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(parent.List.BoardId, card.Id, ActivityType.CardCreated, userId, metadata, DateTime.UtcNow);

        return card;
    }

    public async Task<List<Card>> GetSubCardsAsync(int parentCardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var parent = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == parentCardId && !c.IsDeleted);
        if (parent?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(parent.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Cards
            .Where(c => c.ParentCardId == parentCardId && !c.IsDeleted)
            .OrderBy(c => c.Position)
            .ToListAsync();
    }

    public async Task MoveCardAsync(int cardId, string userId, int targetListId, int position)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var targetList = await context.Lists.FirstOrDefaultAsync(l => l.Id == targetListId && !l.IsDeleted);
        if (targetList == null)
            throw new InvalidOperationException("Target list not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        var sourceListId = card.ListId;
        card.ListId = targetListId;
        card.Position = position;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardMovedAsync(boardId, cardId, sourceListId, targetListId, position);

        var metadata = new { sourceListId, targetListId, position };
        await activityLogService.LogAsync(cardId, userId, ActivityType.CardMoved, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.CardMoved, userId, metadata, DateTime.UtcNow);
    }

    public async Task ReorderCardsAsync(int listId, string userId, List<(int CardId, int Position)> positions)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

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
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.IsDeleted = true;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastCardDeletedAsync(boardId, cardId);

        var metadata = new { };
        await activityLogService.LogAsync(cardId, userId, ActivityType.CardSoftDeleted, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.CardSoftDeleted, userId, metadata, DateTime.UtcNow);
    }

    public async Task RestoreCardAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        card.IsDeleted = false;
        card.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var metadata = new { };
        await activityLogService.LogAsync(cardId, userId, ActivityType.CardRestored, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.CardRestored, userId, metadata, DateTime.UtcNow);
    }

    public async Task AddAssigneeAsync(int cardId, string userId, string assigneeUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var assignee = await context.Users.FirstOrDefaultAsync(u => u.Id == assigneeUserId);
        if (assignee == null)
            throw new InvalidOperationException("User not found");

        var existing = await context.CardAssignees.FirstOrDefaultAsync(ca => ca.CardId == cardId && ca.UserId == assigneeUserId);
        if (existing != null)
            return;

        var boardId = card.List.BoardId;
        var cardAssignee = new CardAssignee { CardId = cardId, UserId = assigneeUserId };
        context.CardAssignees.Add(cardAssignee);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastAssigneeAddedAsync(boardId, cardId, assigneeUserId);

        var metadata = new { assigneeUserId };
        await activityLogService.LogAsync(cardId, userId, ActivityType.AssigneeAdded, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.AssigneeAdded, userId, metadata, DateTime.UtcNow);
    }

    public async Task RemoveAssigneeAsync(int cardId, string userId, string assigneeUserId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        var cardAssignee = await context.CardAssignees.FirstOrDefaultAsync(ca => ca.CardId == cardId && ca.UserId == assigneeUserId);
        if (cardAssignee != null)
        {
            context.CardAssignees.Remove(cardAssignee);
            await context.SaveChangesAsync();
            await boardSyncService.BroadcastAssigneeRemovedAsync(boardId, cardId, assigneeUserId);

            var metadata = new { assigneeUserId };
            await activityLogService.LogAsync(cardId, userId, ActivityType.AssigneeRemoved, metadata);
            await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.AssigneeRemoved, userId, metadata, DateTime.UtcNow);
        }
    }

    public async Task<List<ApplicationUser>> GetCardAssigneesAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.CardAssignees
            .Where(ca => ca.CardId == cardId)
            .Select(ca => ca.User!)
            .ToListAsync();
    }

    public async Task AddLabelAsync(int cardId, string userId, int labelId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var label = await context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && !l.IsDeleted);
        if (label == null)
            throw new InvalidOperationException("Label not found");

        var existing = await context.CardLabels.FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (existing != null)
            return;

        var boardId = card.List.BoardId;
        var cardLabel = new CardLabel { CardId = cardId, LabelId = labelId };
        context.CardLabels.Add(cardLabel);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastLabelAddedAsync(boardId, cardId, labelId);

        var metadata = new { labelId, labelName = label.Name };
        await activityLogService.LogAsync(cardId, userId, ActivityType.LabelAdded, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.LabelAdded, userId, metadata, DateTime.UtcNow);
    }

    public async Task RemoveLabelAsync(int cardId, string userId, int labelId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        var cardLabel = await context.CardLabels.FirstOrDefaultAsync(cl => cl.CardId == cardId && cl.LabelId == labelId);
        if (cardLabel != null)
        {
            context.CardLabels.Remove(cardLabel);
            await context.SaveChangesAsync();
            await boardSyncService.BroadcastLabelRemovedAsync(boardId, cardId, labelId);

            var metadata = new { labelId };
            await activityLogService.LogAsync(cardId, userId, ActivityType.LabelRemoved, metadata);
            await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.LabelRemoved, userId, metadata, DateTime.UtcNow);
        }
    }

    public async Task<List<Label>> GetCardLabelsAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.CardLabels
            .Where(cl => cl.CardId == cardId)
            .Select(cl => cl.Label!)
            .ToListAsync();
    }

    public async Task AddProjectAsync(int cardId, string userId, int projectId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null)
            throw new InvalidOperationException("Project not found");

        var existing = await context.CardProjects.FirstOrDefaultAsync(cp => cp.CardId == cardId && cp.ProjectId == projectId);
        if (existing != null)
            return;

        var boardId = card.List.BoardId;
        var cardProject = new CardProject { CardId = cardId, ProjectId = projectId };
        context.CardProjects.Add(cardProject);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastProjectAddedAsync(boardId, cardId, projectId);

        var metadata = new { projectId, projectName = project.Name };
        await activityLogService.LogAsync(cardId, userId, ActivityType.ProjectAdded, metadata);
        await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.ProjectAdded, userId, metadata, DateTime.UtcNow);
    }

    public async Task RemoveProjectAsync(int cardId, string userId, int projectId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = card.List.BoardId;
        var cardProject = await context.CardProjects.FirstOrDefaultAsync(cp => cp.CardId == cardId && cp.ProjectId == projectId);
        if (cardProject != null)
        {
            context.CardProjects.Remove(cardProject);
            await context.SaveChangesAsync();
            await boardSyncService.BroadcastProjectRemovedAsync(boardId, cardId, projectId);

            var metadata = new { projectId };
            await activityLogService.LogAsync(cardId, userId, ActivityType.ProjectRemoved, metadata);
            await boardSyncService.BroadcastActivityLoggedAsync(boardId, cardId, ActivityType.ProjectRemoved, userId, metadata, DateTime.UtcNow);
        }
    }

    public async Task<List<Project>> GetCardProjectsAsync(int cardId, string userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var card = await context.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId && !c.IsDeleted);
        if (card?.List == null)
            throw new InvalidOperationException("Card not found");

        var isMember = await listService.IsUserBoardMemberAsync(card.List.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.CardProjects
            .Where(cp => cp.CardId == cardId)
            .Select(cp => cp.Project!)
            .ToListAsync();
    }
}
