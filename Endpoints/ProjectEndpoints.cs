using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class ProjectEndpoints
{
    public static void MapProjectEndpoints(this WebApplication app)
    {
        var projectGroup = app.MapGroup("/api/projects").RequireJwtAuthorization();

        projectGroup.MapPost("/", CreateProject)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        projectGroup.MapGet("/", GetAllProjects)
            .Produces<List<object>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> CreateProject(HttpContext context, IProjectService projectService, CreateProjectRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Project name is required");

        try
        {
            var project = await projectService.CreateProjectAsync(request.BoardId, userId, request.Name, request.Color);
            return Results.Created($"/api/projects/{project.Id}", project);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetAllProjects(HttpContext context, IProjectService projectService)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var projects = await projectService.GetAllProjectsAsync();
        return Results.Ok(projects);
    }
}
