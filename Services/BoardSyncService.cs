using Microsoft.AspNetCore.SignalR;
using Kanban.Hubs;

namespace Kanban.Services;

public interface IBoardSyncService
{
    Task BroadcastCardCreatedAsync(int boardId, int cardId, string title, int listId);
    Task BroadcastCardUpdatedAsync(int boardId, int cardId, string title, string description, DateTime? dueDate);
    Task BroadcastCardMovedAsync(int boardId, int cardId, int sourceListId, int targetListId, int position);
    Task BroadcastCardDeletedAsync(int boardId, int cardId);
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
}
