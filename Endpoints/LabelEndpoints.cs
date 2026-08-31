using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class LabelEndpoints
{
    public static void MapLabelEndpoints(this WebApplication app)
    {
        var labelGroup = app.MapGroup("/api/boards/{boardId}/labels").RequireJwtAuthorization();

        labelGroup.MapPost("/", CreateLabel)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        labelGroup.MapGet("/", GetBoardLabels)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        labelGroup.MapDelete("/{labelId}", DeleteLabel)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateLabel(HttpContext context, ILabelService labelService, int boardId, CreateLabelRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Label name is required");

        try
        {
            var label = await labelService.CreateLabelAsync(boardId, userId, request.Name, request.Color);
            return Results.Created($"/api/boards/{boardId}/labels/{label.Id}", label);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetBoardLabels(HttpContext context, ILabelService labelService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var labels = await labelService.GetBoardLabelsAsync(boardId, userId);
            return Results.Ok(labels);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> DeleteLabel(HttpContext context, ILabelService labelService, int boardId, int labelId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await labelService.DeleteLabelAsync(labelId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }
}
