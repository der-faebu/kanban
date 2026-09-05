using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace Kanban.Endpoints;

public static class BoardEndpoints
{
    public static void MapBoardEndpoints(this WebApplication app)
    {
        var boardGroup = app.MapGroup("/api/boards").RequireJwtAuthorization();

        boardGroup.MapPost("/", CreateBoard)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        boardGroup.MapGet("/", GetUserBoards)
            .Produces<List<object>>(StatusCodes.Status200OK);

        boardGroup.MapGet("/{boardId}", GetBoardById)
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        boardGroup.MapDelete("/{boardId}", DeleteBoard)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        boardGroup.MapPost("/{boardId}/members", AddBoardMember)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        boardGroup.MapDelete("/{boardId}/members/{memberId}", RemoveBoardMember)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        boardGroup.MapPut("/{boardId}/members/{memberId}/role", ChangeBoardMemberRole)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        boardGroup.MapGet("/{boardId}/members", GetBoardMembers)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        boardGroup.MapPost("/{boardId}/members/resolve-names", ResolveUserNames)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // A plain browser navigation (an <a href> click) carries the app's Identity cookie, not
        // a JWT bearer token, so this accepts both schemes -- same convention as the attachment
        // download endpoint -- letting the export link work from the browser while the rest of
        // the API stays JWT-only.
        app.MapGet("/api/boards/{boardId}/export", ExportBoard)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser())
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK, contentType: "application/zip")
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> CreateBoard(HttpContext context, IBoardService boardService, CreateBoardRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("Board name is required");

        var board = await boardService.CreateBoardAsync(userId, request.Name, request.Description ?? "");
        return Results.Created($"/api/boards/{board.Id}", board);
    }

    private static async Task<IResult> GetUserBoards(HttpContext context, IBoardService boardService)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var boards = await boardService.GetUserBoardsAsync(userId);
        return Results.Ok(boards);
    }

    private static async Task<IResult> GetBoardById(HttpContext context, IBoardService boardService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var board = await boardService.GetBoardByIdAsync(boardId, userId);
        if (board == null)
            return Results.NotFound();

        return Results.Ok(board);
    }

    private static async Task<IResult> DeleteBoard(HttpContext context, IBoardService boardService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await boardService.DeleteBoardAsync(boardId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("owner") ? Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]) : Results.NotFound();
        }
    }

    private static async Task<IResult> AddBoardMember(HttpContext context, IBoardService boardService, int boardId, AddBoardMemberRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await boardService.AddBoardMemberAsync(boardId, userId, request.MemberId, request.Role);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RemoveBoardMember(HttpContext context, IBoardService boardService, int boardId, string memberId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await boardService.RemoveBoardMemberAsync(boardId, userId, memberId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("owner") ? Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]) : Results.NotFound();
        }
    }

    private static async Task<IResult> ChangeBoardMemberRole(HttpContext context, IBoardService boardService, int boardId, string memberId, ChangeBoardMemberRoleRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await boardService.ChangeBoardMemberRoleAsync(boardId, userId, memberId, request.Role);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetBoardMembers(HttpContext context, IBoardService boardService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var members = await boardService.GetBoardMembersAsync(boardId, userId);
            return Results.Ok(members);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ExportBoard(HttpContext context, IBoardExportService boardExportService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var (zip, boardName) = await boardExportService.ExportBoardAsync(boardId, userId);
            return Results.Stream(zip, "application/zip", $"{boardName}.zip");
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("owner") ? Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]) : Results.NotFound();
        }
    }

    private static async Task<IResult> ResolveUserNames(HttpContext context, IBoardService boardService, int boardId, ResolveUserNamesRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var names = await boardService.GetUsersByIdsAsync(boardId, userId, request.UserIds);
            return Results.Ok(names);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
}
