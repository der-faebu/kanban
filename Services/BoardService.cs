using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IBoardService
{
    Task<Board> CreateBoardAsync(string userId, string name, string description);
    Task<List<Board>> GetUserBoardsAsync(string userId);
    Task<Board?> GetBoardByIdAsync(int boardId, string userId);
    Task DeleteBoardAsync(int boardId, string userId);
    Task AddBoardMemberAsync(int boardId, string userId, string memberId, BoardMemberRole role);
    Task RemoveBoardMemberAsync(int boardId, string userId, string memberId);
    Task ChangeBoardMemberRoleAsync(int boardId, string userId, string memberId, BoardMemberRole role);
    Task<List<BoardMember>> GetBoardMembersAsync(int boardId, string userId);
    Task<bool> IsUserBoardOwnerAsync(int boardId, string userId);
    Task<bool> IsUserBoardMemberAsync(int boardId, string userId);
}

public class BoardService(ApplicationDbContext context) : IBoardService
{
    public async Task<Board> CreateBoardAsync(string userId, string name, string description)
    {
        var board = new Board
        {
            Name = name,
            Description = description,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Boards.Add(board);
        await context.SaveChangesAsync();

        return board;
    }

    public async Task<List<Board>> GetUserBoardsAsync(string userId)
    {
        return await context.Boards
            .Where(b => !b.IsDeleted && (b.OwnerId == userId || b.Members.Any(m => m.UserId == userId)))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<Board?> GetBoardByIdAsync(int boardId, string userId)
    {
        var board = await context.Boards
            .Include(b => b.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            return null;

        if (board.OwnerId != userId && !board.Members.Any(m => m.UserId == userId))
            return null;

        return board;
    }

    public async Task DeleteBoardAsync(int boardId, string userId)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            throw new InvalidOperationException("Board not found");

        if (board.OwnerId != userId)
            throw new InvalidOperationException("Only board owner can delete the board");

        context.Boards.Remove(board);
        await context.SaveChangesAsync();
    }

    public async Task AddBoardMemberAsync(int boardId, string userId, string memberId, BoardMemberRole role)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            throw new InvalidOperationException("Board not found");

        if (board.OwnerId != userId)
            throw new InvalidOperationException("Only board owner can add members");

        if (board.OwnerId == memberId)
            throw new InvalidOperationException("User is already board owner");

        var existingMember = await context.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);

        if (existingMember != null)
            throw new InvalidOperationException("User is already a board member");

        var member = new BoardMember
        {
            BoardId = boardId,
            UserId = memberId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        context.BoardMembers.Add(member);
        await context.SaveChangesAsync();
    }

    public async Task RemoveBoardMemberAsync(int boardId, string userId, string memberId)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            throw new InvalidOperationException("Board not found");

        if (board.OwnerId != userId)
            throw new InvalidOperationException("Only board owner can remove members");

        var member = await context.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);

        if (member == null)
            throw new InvalidOperationException("Member not found");

        context.BoardMembers.Remove(member);
        await context.SaveChangesAsync();
    }

    public async Task ChangeBoardMemberRoleAsync(int boardId, string userId, string memberId, BoardMemberRole role)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            throw new InvalidOperationException("Board not found");

        if (board.OwnerId != userId)
            throw new InvalidOperationException("Only board owner can change member roles");

        var member = await context.BoardMembers
            .FirstOrDefaultAsync(m => m.BoardId == boardId && m.UserId == memberId);

        if (member == null)
            throw new InvalidOperationException("Member not found");

        member.Role = role;
        await context.SaveChangesAsync();
    }

    public async Task<List<BoardMember>> GetBoardMembersAsync(int boardId, string userId)
    {
        var board = await context.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);

        if (board == null)
            throw new InvalidOperationException("Board not found");

        if (board.OwnerId != userId && !await context.BoardMembers.AnyAsync(m => m.BoardId == boardId && m.UserId == userId))
            throw new InvalidOperationException("User is not a board member");

        return await context.BoardMembers
            .Where(m => m.BoardId == boardId)
            .Include(m => m.User)
            .ToListAsync();
    }

    public async Task<bool> IsUserBoardOwnerAsync(int boardId, string userId)
    {
        return await context.Boards
            .AnyAsync(b => b.Id == boardId && !b.IsDeleted && b.OwnerId == userId);
    }

    public async Task<bool> IsUserBoardMemberAsync(int boardId, string userId)
    {
        return await context.Boards
            .AnyAsync(b => b.Id == boardId && !b.IsDeleted &&
                (b.OwnerId == userId || b.Members.Any(m => m.UserId == userId)));
    }
}
