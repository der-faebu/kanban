using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface ILabelService
{
    Task<Label> CreateLabelAsync(int boardId, string userId, string name, string color);
    Task<List<Label>> GetBoardLabelsAsync(int boardId, string userId);
    Task DeleteLabelAsync(int labelId, string userId);
}

public class LabelService(ApplicationDbContext context, IListService listService, IBoardSyncService boardSyncService) : ILabelService
{
    public async Task<Label> CreateLabelAsync(int boardId, string userId, string name, string color)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);
        if (board == null)
            throw new InvalidOperationException("Board not found");

        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var label = new Label
        {
            BoardId = boardId,
            Name = name,
            Color = color,
            IsDeleted = false
        };

        context.Labels.Add(label);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastLabelCreatedAsync(boardId, label.Id, label.Name, label.Color);
        return label;
    }

    public async Task<List<Label>> GetBoardLabelsAsync(int boardId, string userId)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);
        if (board == null)
            throw new InvalidOperationException("Board not found");

        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Labels
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .ToListAsync();
    }

    public async Task DeleteLabelAsync(int labelId, string userId)
    {
        var label = await context.Labels.FirstOrDefaultAsync(l => l.Id == labelId && !l.IsDeleted);
        if (label == null)
            throw new InvalidOperationException("Label not found");

        var isMember = await listService.IsUserBoardMemberAsync(label.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var boardId = label.BoardId;
        label.IsDeleted = true;
        label.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastLabelDeletedAsync(boardId, labelId);
    }
}
