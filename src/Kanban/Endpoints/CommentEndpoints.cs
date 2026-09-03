using System.Security.Claims;
using Kanban.Data;
using Kanban.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kanban.Endpoints;

public static class CommentEndpoints
{
    public static void MapCommentEndpoints(this WebApplication app)
    {
        var commentGroup = app.MapGroup("/api/lists/{listId}/cards/{cardId}/comments").RequireJwtAuthorization();

        commentGroup.MapPost("/", CreateComment)
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        commentGroup.MapGet("/", GetComments)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        commentGroup.MapPut("/{commentId}", UpdateComment)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden);

        commentGroup.MapDelete("/{commentId}", DeleteComment)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> CreateComment(HttpContext context, ICommentService commentService, int listId, int cardId, CreateCommentRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest("Comment text is required");

        try
        {
            var comment = await commentService.AddCommentAsync(cardId, userId, request.Text);
            return Results.Created($"/api/lists/{listId}/cards/{cardId}/comments/{comment.Id}", comment);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetComments(HttpContext context, ICommentService commentService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var comments = await commentService.GetCardCommentsAsync(cardId, userId);
            return Results.Ok(comments);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> UpdateComment(HttpContext context, ICommentService commentService, int listId, int cardId, int commentId, UpdateCommentRequest request)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Text))
            return Results.BadRequest("Comment text is required");

        try
        {
            await commentService.UpdateCommentAsync(commentId, userId, request.Text);
            return Results.Ok();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return Results.NotFound();
            if (ex.Message.Contains("Only the comment author"))
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> DeleteComment(HttpContext context, ICommentService commentService, int listId, int cardId, int commentId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await commentService.DeleteCommentAsync(commentId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found"))
                return Results.NotFound();
            if (ex.Message.Contains("Only the comment author or board owner"))
                return Results.Forbid(authenticationSchemes: [JwtBearerDefaults.AuthenticationScheme]);
            return Results.BadRequest(ex.Message);
        }
    }
}
