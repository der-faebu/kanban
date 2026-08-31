using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class ListEndpoints
{
    public static void MapListEndpoints(this WebApplication app)
    {
        var listGroup = app.MapGroup("/api/boards/{boardId}/lists").RequireJwtAuthorization();

        listGroup.MapPost("/", CreateList)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        listGroup.MapGet("/", GetBoardLists)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        listGroup.MapGet("/{listId}", GetListById)
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        listGroup.MapPut("/{listId}/name", RenameList)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        listGroup.MapPut("/reorder", ReorderLists)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        listGroup.MapDelete("/{listId}", SoftDeleteList)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        listGroup.MapPost("/{listId}/restore", RestoreList)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateList(HttpContext context, IListService listService, int boardId, CreateListRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("List name is required");

        try
        {
            var list = await listService.CreateListAsync(boardId, userId, request.Name);
            return Results.Created($"/api/boards/{boardId}/lists/{list.Id}", list);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetBoardLists(HttpContext context, IListService listService, int boardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var lists = await listService.GetBoardListsAsync(boardId, userId);
            return Results.Ok(lists);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetListById(HttpContext context, IListService listService, int boardId, int listId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var list = await listService.GetListByIdAsync(listId, userId);
        if (list == null || list.BoardId != boardId)
            return Results.NotFound();

        return Results.Ok(list);
    }

    private static async Task<IResult> RenameList(HttpContext context, IListService listService, int boardId, int listId, RenameListRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await listService.RenameListAsync(listId, userId, request.Name);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ReorderLists(HttpContext context, IListService listService, int boardId, ReorderListsRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await listService.ReorderListsAsync(boardId, userId, request.Positions);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SoftDeleteList(HttpContext context, IListService listService, int boardId, int listId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await listService.SoftDeleteListAsync(listId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RestoreList(HttpContext context, IListService listService, int boardId, int listId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await listService.RestoreListAsync(listId, userId);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }
}
