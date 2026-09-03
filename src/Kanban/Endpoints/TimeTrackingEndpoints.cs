using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kanban.Endpoints;

public static class TimeTrackingEndpoints
{
    public static void MapTimeTrackingEndpoints(this WebApplication app)
    {
        var timeEntryGroup = app.MapGroup("/api/lists/{listId}/cards/{cardId}/time-entries").RequireJwtAuthorization();

        timeEntryGroup.MapPost("/", CreateTimeLogEntry)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        timeEntryGroup.MapGet("/", GetTimeLogEntries)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        timeEntryGroup.MapPut("/{entryId}", UpdateTimeLogEntry)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        timeEntryGroup.MapDelete("/{entryId}", DeleteTimeLogEntry)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateTimeLogEntry(HttpContext context, ITimeTrackingService timeTrackingService, int listId, int cardId, CreateTimeLogEntryRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (request.DurationHours <= 0)
            return Results.BadRequest("Duration must be greater than zero");

        try
        {
            var entry = await timeTrackingService.AddTimeLogEntryAsync(cardId, userId, request.Date, request.DurationHours, request.Note);
            return Results.Created($"/api/lists/{listId}/cards/{cardId}/time-entries/{entry.Id}", entry);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetTimeLogEntries(HttpContext context, ITimeTrackingService timeTrackingService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var entries = await timeTrackingService.GetCardTimeLogEntriesAsync(cardId, userId);
            return Results.Ok(entries);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateTimeLogEntry(HttpContext context, ITimeTrackingService timeTrackingService, int listId, int cardId, int entryId, UpdateTimeLogEntryRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (request.DurationHours <= 0)
            return Results.BadRequest("Duration must be greater than zero");

        try
        {
            await timeTrackingService.UpdateTimeLogEntryAsync(entryId, userId, request.Date, request.DurationHours, request.Note);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return Results.NotFound();
            if (ex.Message.Contains("Only the entry author"))
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> DeleteTimeLogEntry(HttpContext context, ITimeTrackingService timeTrackingService, int listId, int cardId, int entryId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await timeTrackingService.DeleteTimeLogEntryAsync(entryId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return Results.NotFound();
            if (ex.Message.Contains("Only the entry author or board owner"))
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            return Results.BadRequest(ex.Message);
        }
    }
}
