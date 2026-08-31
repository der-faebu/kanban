using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class ChecklistEndpoints
{
    public static void MapChecklistEndpoints(this WebApplication app)
    {
        var checklistGroup = app.MapGroup("/api/lists/{listId}/cards/{cardId}/checklist-items").RequireJwtAuthorization();

        checklistGroup.MapPost("/", AddItem)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        checklistGroup.MapGet("/", GetItems)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        checklistGroup.MapPut("/reorder", ReorderItems)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        checklistGroup.MapPut("/{itemId}", UpdateItemText)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        checklistGroup.MapPut("/{itemId}/toggle", ToggleItem)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        checklistGroup.MapDelete("/{itemId}", DeleteItem)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> AddItem(HttpContext context, IChecklistService checklistService, int listId, int cardId, CreateChecklistItemRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest("Checklist item text is required");

        try
        {
            var item = await checklistService.AddItemAsync(cardId, userId, request.Text);
            return Results.Created($"/api/lists/{listId}/cards/{cardId}/checklist-items/{item.Id}", item);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetItems(HttpContext context, IChecklistService checklistService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var items = await checklistService.GetItemsAsync(cardId, userId);
            return Results.Ok(items);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateItemText(HttpContext context, IChecklistService checklistService, int listId, int cardId, int itemId, UpdateChecklistItemRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest("Checklist item text is required");

        try
        {
            await checklistService.UpdateItemTextAsync(itemId, userId, request.Text);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ToggleItem(HttpContext context, IChecklistService checklistService, int listId, int cardId, int itemId, ToggleChecklistItemRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await checklistService.ToggleItemAsync(itemId, userId, request.IsDone);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> DeleteItem(HttpContext context, IChecklistService checklistService, int listId, int cardId, int itemId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await checklistService.DeleteItemAsync(itemId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ReorderItems(HttpContext context, IChecklistService checklistService, int listId, int cardId, ReorderChecklistItemsRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var positions = request.Positions.Select(p => (p.ItemId, p.Position)).ToList();
            await checklistService.ReorderItemsAsync(cardId, userId, positions);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }
}
