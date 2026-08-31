using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        var cardGroup = app.MapGroup("/api/lists/{listId}/cards").RequireAuthorization();

        cardGroup.MapPost("/", CreateCard)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapGet("/", GetListCards)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapGet("/{cardId}", GetCardById)
            .Produces<object>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPut("/{cardId}", UpdateCard)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPost("/{cardId}/move", MoveCard)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapPut("/reorder", ReorderCards)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapDelete("/{cardId}", SoftDeleteCard)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPost("/{cardId}/restore", RestoreCard)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateCard(HttpContext context, ICardService cardService, int listId, CreateCardRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Card title is required");

        try
        {
            var card = await cardService.CreateCardAsync(listId, userId, request.Title, request.Description ?? "");
            return Results.Created($"/api/lists/{listId}/cards/{card.Id}", card);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetListCards(HttpContext context, ICardService cardService, int listId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var cards = await cardService.GetListCardsAsync(listId, userId);
            return Results.Ok(cards);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetCardById(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var card = await cardService.GetCardByIdAsync(cardId, userId);
        if (card == null || card.ListId != listId)
            return Results.NotFound();

        return Results.Ok(card);
    }

    private static async Task<IResult> UpdateCard(HttpContext context, ICardService cardService, int listId, int cardId, UpdateCardRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.UpdateCardAsync(cardId, userId, request.Title, request.Description ?? "");
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> MoveCard(HttpContext context, ICardService cardService, int listId, int cardId, MoveCardRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.MoveCardAsync(cardId, userId, request.TargetListId, request.Position);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> ReorderCards(HttpContext context, ICardService cardService, int listId, ReorderCardsRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.ReorderCardsAsync(listId, userId, request.Positions);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SoftDeleteCard(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SoftDeleteCardAsync(cardId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RestoreCard(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.RestoreCardAsync(cardId, userId);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }
}
