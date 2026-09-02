using Microsoft.AspNetCore.SignalR;
using Kanban.Data.Entities;
using Kanban.Hubs;

namespace Kanban.Services;

public interface IBoardSyncService
{
    Task BroadcastCardCreatedAsync(int boardId, int cardId, string title, int listId);
    Task BroadcastCardUpdatedAsync(int boardId, int cardId, string title, string description, DateTime? dueDate);
    Task BroadcastCardMovedAsync(int boardId, int cardId, int sourceListId, int targetListId, int position);
    Task BroadcastCardDeletedAsync(int boardId, int cardId);
    Task BroadcastCardPriorityChangedAsync(int boardId, int cardId, CardPriority? priority);
    Task BroadcastCardStateChangedAsync(int boardId, int cardId, CardState state);
    Task BroadcastCardTypeChangedAsync(int boardId, int cardId, CardType? type);
    Task BroadcastCardDevReferenceUrlChangedAsync(int boardId, int cardId, string? url);
    Task BroadcastCardTicketUrlChangedAsync(int boardId, int cardId, string? url);
    Task BroadcastListCreatedAsync(int boardId, int listId, string name);
    Task BroadcastListUpdatedAsync(int boardId, int listId, string name);
    Task BroadcastListReorderedAsync(int boardId, List<(int ListId, int Position)> positions);
    Task BroadcastListDeletedAsync(int boardId, int listId);
    Task BroadcastAssigneeAddedAsync(int boardId, int cardId, string userId);
    Task BroadcastAssigneeRemovedAsync(int boardId, int cardId, string userId);
    Task BroadcastLabelAddedAsync(int boardId, int cardId, int labelId);
    Task BroadcastLabelRemovedAsync(int boardId, int cardId, int labelId);
    Task BroadcastLabelCreatedAsync(int boardId, int labelId, string name, string color);
    Task BroadcastLabelDeletedAsync(int boardId, int labelId);
    Task BroadcastProjectCreatedAsync(int boardId, int projectId, string name, string color);
    Task BroadcastProjectAddedAsync(int boardId, int cardId, int projectId);
    Task BroadcastProjectRemovedAsync(int boardId, int cardId, int projectId);
    Task BroadcastCommentAddedAsync(int boardId, int cardId, int commentId, string authorId, string text, DateTime createdAt);
    Task BroadcastCommentUpdatedAsync(int boardId, int cardId, int commentId, string text, DateTime updatedAt);
    Task BroadcastCommentDeletedAsync(int boardId, int cardId, int commentId);
    Task BroadcastChecklistItemAddedAsync(int boardId, int cardId, int itemId, string text, int position);
    Task BroadcastChecklistItemUpdatedAsync(int boardId, int cardId, int itemId, string text, bool isDone);
    Task BroadcastChecklistItemDeletedAsync(int boardId, int cardId, int itemId);
    Task BroadcastChecklistReorderedAsync(int boardId, int cardId, List<(int ItemId, int Position)> positions);
    Task BroadcastAttachmentAddedAsync(int boardId, int cardId, int attachmentId, string fileName, string uploadedByUserId);
    Task BroadcastAttachmentDeletedAsync(int boardId, int cardId, int attachmentId);
    Task BroadcastActivityLoggedAsync(int boardId, int cardId, ActivityType activityType, string userId, object metadata, DateTime createdAt);
}

public class BoardSyncService(IHubContext<BoardSyncHub> hubContext) : IBoardSyncService
{
    public async Task BroadcastCardCreatedAsync(int boardId, int cardId, string title, int listId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardCreated", new { cardId, title, listId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardUpdatedAsync(int boardId, int cardId, string title, string description, DateTime? dueDate)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardUpdated", new { cardId, title, description, dueDate, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardMovedAsync(int boardId, int cardId, int sourceListId, int targetListId, int position)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardMoved", new { cardId, sourceListId, targetListId, position, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardDeletedAsync(int boardId, int cardId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardDeleted", new { cardId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardPriorityChangedAsync(int boardId, int cardId, CardPriority? priority)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardPriorityChanged", new { cardId, priority, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardStateChangedAsync(int boardId, int cardId, CardState state)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardStateChanged", new { cardId, state, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardTypeChangedAsync(int boardId, int cardId, CardType? type)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardTypeChanged", new { cardId, type, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardDevReferenceUrlChangedAsync(int boardId, int cardId, string? url)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardDevReferenceUrlChanged", new { cardId, url, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCardTicketUrlChangedAsync(int boardId, int cardId, string? url)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CardTicketUrlChanged", new { cardId, url, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastListCreatedAsync(int boardId, int listId, string name)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ListCreated", new { listId, name, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastListUpdatedAsync(int boardId, int listId, string name)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ListUpdated", new { listId, name, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastListReorderedAsync(int boardId, List<(int ListId, int Position)> positions)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ListReordered", new { positions, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastListDeletedAsync(int boardId, int listId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ListDeleted", new { listId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastAssigneeAddedAsync(int boardId, int cardId, string userId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("AssigneeAdded", new { cardId, userId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastAssigneeRemovedAsync(int boardId, int cardId, string userId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("AssigneeRemoved", new { cardId, userId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastLabelAddedAsync(int boardId, int cardId, int labelId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("LabelAdded", new { cardId, labelId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastLabelRemovedAsync(int boardId, int cardId, int labelId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("LabelRemoved", new { cardId, labelId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastLabelCreatedAsync(int boardId, int labelId, string name, string color)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("LabelCreated", new { labelId, name, color, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastLabelDeletedAsync(int boardId, int labelId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("LabelDeleted", new { labelId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastProjectCreatedAsync(int boardId, int projectId, string name, string color)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ProjectCreated", new { projectId, name, color, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastProjectAddedAsync(int boardId, int cardId, int projectId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ProjectAdded", new { cardId, projectId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastProjectRemovedAsync(int boardId, int cardId, int projectId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ProjectRemoved", new { cardId, projectId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCommentAddedAsync(int boardId, int cardId, int commentId, string authorId, string text, DateTime createdAt)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CommentAdded", new { cardId, commentId, authorId, text, createdAt, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCommentUpdatedAsync(int boardId, int cardId, int commentId, string text, DateTime updatedAt)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CommentUpdated", new { cardId, commentId, text, updatedAt, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastCommentDeletedAsync(int boardId, int cardId, int commentId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("CommentDeleted", new { cardId, commentId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastChecklistItemAddedAsync(int boardId, int cardId, int itemId, string text, int position)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ChecklistItemAdded", new { cardId, itemId, text, position, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastChecklistItemUpdatedAsync(int boardId, int cardId, int itemId, string text, bool isDone)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ChecklistItemUpdated", new { cardId, itemId, text, isDone, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastChecklistItemDeletedAsync(int boardId, int cardId, int itemId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ChecklistItemDeleted", new { cardId, itemId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastChecklistReorderedAsync(int boardId, int cardId, List<(int ItemId, int Position)> positions)
    {
        // System.Text.Json only serializes public properties, not the public fields ValueTuple
        // exposes, so the tuples are mapped to named objects here before going out over SignalR
        // (the tuple shape is kept on the service signature to mirror ICardService's convention).
        var payload = positions.Select(p => new { itemId = p.ItemId, position = p.Position });
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ChecklistReordered", new { cardId, positions = payload, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastAttachmentAddedAsync(int boardId, int cardId, int attachmentId, string fileName, string uploadedByUserId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("AttachmentAdded", new { cardId, attachmentId, fileName, uploadedByUserId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastAttachmentDeletedAsync(int boardId, int cardId, int attachmentId)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("AttachmentDeleted", new { cardId, attachmentId, timestamp = DateTime.UtcNow });
    }

    public async Task BroadcastActivityLoggedAsync(int boardId, int cardId, ActivityType activityType, string userId, object metadata, DateTime createdAt)
    {
        await hubContext.Clients.Group($"board-{boardId}")
            .SendAsync("ActivityLogged", new { cardId, activityType = activityType.ToString(), userId, metadata, createdAt, timestamp = DateTime.UtcNow });
    }
}
