using Kanban.Data;
using Kanban.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Services;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(int? boardId, string userId, string name, string color);
    Task<List<Project>> GetAllProjectsAsync();
    Task UpdateProjectAsync(int projectId, string name, string color);
    Task DeleteProjectAsync(int projectId);
}

public class ProjectService(IDbContextFactory<ApplicationDbContext> contextFactory, IListService listService, IBoardSyncService boardSyncService) : IProjectService
{
    // Projects are global (not board-scoped), and can be created from two different places:
    // a card's detail view (boardId is that card's board -- membership there gates creation,
    // and doubles as the SignalR group to notify, matching LabelService.CreateLabelAsync's
    // shape) or the standalone global Projects page (boardId is null -- no board to check
    // membership against or notify, so creation there only requires being authenticated,
    // already enforced by the caller).
    public async Task<Project> CreateProjectAsync(int? boardId, string userId, string name, string color)
    {
        if (boardId.HasValue)
        {
            var isMember = await listService.IsUserBoardMemberAsync(boardId.Value, userId);
            if (!isMember)
                throw new InvalidOperationException("User is not a board member");
        }

        await using var context = await contextFactory.CreateDbContextAsync();

        var project = new Project
        {
            Name = name,
            Color = color
        };

        context.Projects.Add(project);
        await context.SaveChangesAsync();

        if (boardId.HasValue)
        {
            await boardSyncService.BroadcastProjectCreatedAsync(boardId.Value, project.Id, project.Name, project.Color);
        }

        return project;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Projects
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    // Projects have no owner/creator field -- any authenticated user may rename or delete
    // any project, matching the same "no admin-only gate" model as creation.
    public async Task UpdateProjectAsync(int projectId, string name, string color)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
        if (project == null)
            throw new InvalidOperationException("Project not found");

        project.Name = name;
        project.Color = color;
        project.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task DeleteProjectAsync(int projectId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && !p.IsDeleted);
        if (project == null)
            throw new InvalidOperationException("Project not found");

        project.IsDeleted = true;
        project.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
