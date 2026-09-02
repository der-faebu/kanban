using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        var cardGroup = app.MapGroup("/api/lists/{listId}/cards").RequireJwtAuthorization();

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

        cardGroup.MapPut("/{cardId}/state", SetCardState)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPut("/{cardId}/type", SetCardType)
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

        cardGroup.MapPost("/{cardId}/assignees", AddAssignee)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapDelete("/{cardId}/assignees/{assigneeUserId}", RemoveAssignee)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapGet("/{cardId}/assignees", GetAssignees)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPost("/{cardId}/labels", AddLabel)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapDelete("/{cardId}/labels/{labelId}", RemoveLabel)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapGet("/{cardId}/labels", GetLabels)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPost("/{cardId}/projects", AddProject)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapDelete("/{cardId}/projects/{projectId}", RemoveProject)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapGet("/{cardId}/projects", GetProjects)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPut("/{cardId}/dev-link", SetDevReferenceUrl)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPut("/{cardId}/ticket-link", SetTicketUrl)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPut("/{cardId}/estimate", SetEstimatedHours)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        cardGroup.MapPost("/{cardId}/subcards", CreateSubCard)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        cardGroup.MapGet("/{cardId}/subcards", GetSubCards)
            .Produces<List<object>>(StatusCodes.Status200OK)
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
            await cardService.UpdateCardAsync(cardId, userId, request.Title, request.Description ?? "", request.DueDate);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SetCardState(HttpContext context, ICardService cardService, int listId, int cardId, SetCardStateRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SetCardStateAsync(cardId, userId, request.State);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SetCardType(HttpContext context, ICardService cardService, int listId, int cardId, SetCardTypeRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SetCardTypeAsync(cardId, userId, request.Type);
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

    private static async Task<IResult> AddAssignee(HttpContext context, ICardService cardService, int listId, int cardId, AddCardAssigneeRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.AddAssigneeAsync(cardId, userId, request.UserId);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RemoveAssignee(HttpContext context, ICardService cardService, int listId, int cardId, string assigneeUserId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.RemoveAssigneeAsync(cardId, userId, assigneeUserId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetAssignees(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var assignees = await cardService.GetCardAssigneesAsync(cardId, userId);
            return Results.Ok(assignees);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AddLabel(HttpContext context, ICardService cardService, int listId, int cardId, AddCardLabelRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.AddLabelAsync(cardId, userId, request.LabelId);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RemoveLabel(HttpContext context, ICardService cardService, int listId, int cardId, int labelId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.RemoveLabelAsync(cardId, userId, labelId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetLabels(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var labels = await cardService.GetCardLabelsAsync(cardId, userId);
            return Results.Ok(labels);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AddProject(HttpContext context, ICardService cardService, int listId, int cardId, AddCardProjectRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.AddProjectAsync(cardId, userId, request.ProjectId);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> RemoveProject(HttpContext context, ICardService cardService, int listId, int cardId, int projectId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.RemoveProjectAsync(cardId, userId, projectId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetProjects(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var projects = await cardService.GetCardProjectsAsync(cardId, userId);
            return Results.Ok(projects);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> SetDevReferenceUrl(HttpContext context, ICardService cardService, int listId, int cardId, SetCardDevReferenceUrlRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SetDevReferenceUrlAsync(cardId, userId, request.Url);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SetTicketUrl(HttpContext context, ICardService cardService, int listId, int cardId, SetCardTicketUrlRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SetTicketUrlAsync(cardId, userId, request.Url);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> SetEstimatedHours(HttpContext context, ICardService cardService, int listId, int cardId, SetCardEstimateRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await cardService.SetEstimatedHoursAsync(cardId, userId, request.EstimatedHours);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> CreateSubCard(HttpContext context, ICardService cardService, int listId, int cardId, CreateCardRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Card title is required");

        try
        {
            var card = await cardService.CreateSubCardAsync(cardId, userId, request.Title, request.Description ?? "");
            return Results.Created($"/api/lists/{card.ListId}/cards/{card.Id}", card);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetSubCards(HttpContext context, ICardService cardService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var subCards = await cardService.GetSubCardsAsync(cardId, userId);
            return Results.Ok(subCards);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
}
