using System.Security.Claims;
using Kanban.Services;

namespace Kanban.Endpoints;

public static class ActivityLogEndpoints
{
    public static void MapActivityLogEndpoints(this WebApplication app)
    {
        var activityGroup = app.MapGroup("/api/lists/{listId}/cards/{cardId}/activity").RequireJwtAuthorization();

        activityGroup.MapGet("/", GetActivity)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetActivity(HttpContext context, IActivityLogService activityLogService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var activity = await activityLogService.GetCardActivityAsync(cardId, userId);
            return Results.Ok(activity);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
}
