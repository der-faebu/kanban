using Kanban.Data;
using Kanban.Data.Dtos;
using Kanban.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Endpoints;

public static class TrelloImportEndpoints
{
    public static void MapTrelloImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import")
            .WithName("TrelloImport")
            .RequireJwtAuthorization();

        group.MapPost("/trello/{boardId}", ImportTrello)
            .WithName("ImportTrello")
            .WithDescription("Import a Trello board JSON export into an existing board")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<TrelloImportResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .DisableAntiforgery();
    }

    private static async Task<IResult> ImportTrello(
        int boardId,
        IFormFile file,
        ITrelloImportService trelloImportService,
        ApplicationDbContext dbContext,
        HttpContext context)
    {
        if (file == null || file.Length == 0)
            return Results.BadRequest(new { message = "No file provided" });

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { message = "File must be a JSON file" });

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var board = await dbContext.Boards.FirstOrDefaultAsync(b => b.Id == boardId && !b.IsDeleted);
        if (board == null)
            return Results.NotFound(new { message = "Board not found" });

        if (board.OwnerId != userId)
            return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);

        try
        {
            using var stream = file.OpenReadStream();
            var result = await trelloImportService.ImportBoardAsync(userId, stream, board.Name);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = $"Import failed: {ex.Message}" });
        }
    }
}
