using Kanban.Data.Dtos;
using Kanban.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kanban.Endpoints;

public static class BoardImportEndpoints
{
    public static void MapBoardImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import")
            .WithName("BoardImport")
            .RequireJwtAuthorization();

        group.MapPost("/board", ImportBoard)
            .WithName("ImportBoard")
            .WithDescription("Restore a brand-new board from a zip produced by the board export feature")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<BoardImportResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery();
    }

    private static async Task<IResult> ImportBoard(
        IFormFile file,
        [FromForm] string boardName,
        IBoardImportService boardImportService,
        HttpContext context)
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { message = "No file provided" });

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "File must be a zip archive" });

        if (string.IsNullOrWhiteSpace(boardName))
            return Results.BadRequest(new { message = "Board name is required" });

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            using var stream = file.OpenReadStream();
            var result = await boardImportService.ImportBoardAsync(userId, stream, boardName);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = $"Import failed: {ex.Message}" });
        }
    }
}
