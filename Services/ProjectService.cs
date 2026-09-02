using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(int boardId, string userId, string name, string color);
    Task<List<Project>> GetAllProjectsAsync();
}

public class ProjectService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardSyncService boardSyncService) : IProjectService
{
    // Projects are global (not board-scoped) once created, but creation itself is still gated to
    // members of the board the card was opened from -- boardId doubles as that authorization check
    // and as the SignalR group to notify, matching LabelService.CreateLabelAsync's shape.
    public async Task<Project> CreateProjectAsync(int boardId, string userId, string name, string color)
    {
        var isMember = await listService.IsUserBoardMemberAsync(boardId, userId);
        if (!isMember)
            throw new InvalidOperationException("User is not a board member");

        await using var context = await contextFactory.CreateDbContextAsync();

        var project = new Project
        {
            Name = name,
            Color = color
        };

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        await boardSyncService.BroadcastProjectCreatedAsync(boardId, project.Id, project.Name, project.Color);
        return project;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Projects
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
