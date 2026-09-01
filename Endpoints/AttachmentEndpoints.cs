using System.Security.Claims;
using Kanban.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;

namespace Kanban.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var attachmentGroup = app.MapGroup("/api/lists/{listId}/cards/{cardId}/attachments").RequireJwtAuthorization();

        attachmentGroup.MapPost("/", UploadAttachment)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        attachmentGroup.MapGet("/", GetAttachments)
            .Produces<List<object>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        attachmentGroup.MapDelete("/{attachmentId}", DeleteAttachment)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Downloads are followed as plain browser navigations (an <a href> click from the
        // card detail page), which carries the app's Identity cookie, not a JWT bearer token.
        // Accepting both schemes here lets this one endpoint work from the browser while the
        // rest of the API stays JWT-only, matching the existing convention and test harness.
        app.MapGet("/api/lists/{listId}/cards/{cardId}/attachments/{attachmentId}/download", DownloadAttachment)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, IdentityConstants.ApplicationScheme)
                .RequireAuthenticatedUser())
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> UploadAttachment(HttpContext context, IAttachmentService attachmentService, int listId, int cardId, IFormFile? file)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided");

        try
        {
            using var stream = file.OpenReadStream();
            var attachment = await attachmentService.UploadAttachmentAsync(cardId, userId, stream, file.FileName, file.ContentType, file.Length);
            return Results.Created($"/api/lists/{listId}/cards/{cardId}/attachments/{attachment.Id}", attachment);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetAttachments(HttpContext context, IAttachmentService attachmentService, int listId, int cardId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var attachments = await attachmentService.GetAttachmentsAsync(cardId, userId);
            return Results.Ok(attachments);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> DownloadAttachment(HttpContext context, IAttachmentService attachmentService, int listId, int cardId, int attachmentId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var (stream, fileName, contentType) = await attachmentService.GetAttachmentStreamAsync(attachmentId, userId);
            return Results.Stream(stream, contentType, fileName);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
        catch (FileNotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> DeleteAttachment(HttpContext context, IAttachmentService attachmentService, int listId, int cardId, int attachmentId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await attachmentService.DeleteAttachmentAsync(attachmentId, userId);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains("not found") ? Results.NotFound() : Results.BadRequest(ex.Message);
        }
    }
}
