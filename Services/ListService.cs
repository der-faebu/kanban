using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IListService
{
    Task<List> CreateListAsync(int boardId, string userId, string name);
    Task<List?> GetListByIdAsync(int listId, string userId);
    Task<List<List>> GetBoardListsAsync(int boardId, string userId);
    Task RenameListAsync(int listId, string userId, string newName);
    Task ReorderListsAsync(int boardId, string userId, List<(int ListId, int Position)> positions);
    Task SoftDeleteListAsync(int listId, string userId);
    Task RestoreListAsync(int listId, string userId);
    Task<bool> IsUserBoardMemberAsync(int boardId, string userId);
}

public class ListService(ApplicationDbContext context, IBoardService boardService) : IListService
{
    public async Task<List> CreateListAsync(int boardId, string userId, string name)
    {
        var isMember = await boardService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var maxPosition = await context.Lists
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .MaxAsync(l => (int?)l.Position) ?? -1;

        var list = new List
        {
            BoardId = boardId,
            Name = name,
            Position = maxPosition + 1,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Lists.Add(list);
        await context.SaveChangesAsync();

        return list;
    }

    public async Task<List?> GetListByIdAsync(int listId, string userId)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);

        if (list == null)
            return null;

        var isMember = await boardService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            return null;

        return list;
    }

    public async Task<List<List>> GetBoardListsAsync(int boardId, string userId)
    {
        var isMember = await boardService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        return await context.Lists
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task RenameListAsync(int listId, string userId, string newName)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);

        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await boardService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        list.Name = newName;
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task ReorderListsAsync(int boardId, string userId, List<(int ListId, int Position)> positions)
    {
        var isMember = await boardService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        var lists = await context.Lists
            .Where(l => l.BoardId == boardId && !l.IsDeleted)
            .ToListAsync();

        foreach (var (listId, position) in positions)
        {
            var list = lists.FirstOrDefault(l => l.Id == listId);
            if (list != null)
            {
                list.Position = position;
                list.UpdatedAt = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
    }

    public async Task SoftDeleteListAsync(int listId, string userId)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && !l.IsDeleted);

        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await boardService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        list.IsDeleted = true;
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task RestoreListAsync(int listId, string userId)
    {
        var list = await context.Lists.FirstOrDefaultAsync(l => l.Id == listId && l.IsDeleted);

        if (list == null)
            throw new InvalidOperationException("List not found");

        var isMember = await boardService.IsUserBoardMemberAsync(list.BoardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        list.IsDeleted = false;
        list.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<bool> IsUserBoardMemberAsync(int boardId, string userId)
    {
        return await boardService.IsUserBoardMemberAsync(boardId, userId);
    }
}
