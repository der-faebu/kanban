using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Kanban.Components;
using Kanban.Components.Account;
using Kanban.Data;
using Kanban.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<IBoardService, BoardService>();
builder.Services.AddScoped<IListService, ListService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Board management endpoints
var boardGroup = app.MapGroup("/api/boards").RequireAuthorization();

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

async Task<IResult> CreateBoard(HttpContext context, IBoardService boardService, CreateBoardRequest request)
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest("Board name is required");

    var board = await boardService.CreateBoardAsync(userId, request.Name, request.Description ?? "");
    return Results.Created($"/api/boards/{board.Id}", board);
}

async Task<IResult> GetUserBoards(HttpContext context, IBoardService boardService)
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var boards = await boardService.GetUserBoardsAsync(userId);
    return Results.Ok(boards);
}

async Task<IResult> GetBoardById(HttpContext context, IBoardService boardService, int boardId)
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var board = await boardService.GetBoardByIdAsync(boardId, userId);
    if (board == null)
        return Results.NotFound();

    return Results.Ok(board);
}

async Task<IResult> DeleteBoard(HttpContext context, IBoardService boardService, int boardId)
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
        return ex.Message.Contains("owner") ? Results.Forbid() : Results.NotFound();
    }
}

async Task<IResult> AddBoardMember(HttpContext context, IBoardService boardService, int boardId, AddBoardMemberRequest request)
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

async Task<IResult> RemoveBoardMember(HttpContext context, IBoardService boardService, int boardId, string memberId)
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
        return ex.Message.Contains("owner") ? Results.Forbid() : Results.NotFound();
    }
}

async Task<IResult> ChangeBoardMemberRole(HttpContext context, IBoardService boardService, int boardId, string memberId, ChangeBoardMemberRoleRequest request)
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

async Task<IResult> GetBoardMembers(HttpContext context, IBoardService boardService, int boardId)
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

// List management endpoints
var listGroup = app.MapGroup("/api/boards/{boardId}/lists").RequireAuthorization();

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

async Task<IResult> CreateList(HttpContext context, IListService listService, int boardId, CreateListRequest request)
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

async Task<IResult> GetBoardLists(HttpContext context, IListService listService, int boardId)
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

async Task<IResult> GetListById(HttpContext context, IListService listService, int boardId, int listId)
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrEmpty(userId))
        return Results.Unauthorized();

    var list = await listService.GetListByIdAsync(listId, userId);
    if (list == null || list.BoardId != boardId)
        return Results.NotFound();

    return Results.Ok(list);
}

async Task<IResult> RenameList(HttpContext context, IListService listService, int boardId, int listId, RenameListRequest request)
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

async Task<IResult> ReorderLists(HttpContext context, IListService listService, int boardId, ReorderListsRequest request)
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

async Task<IResult> SoftDeleteList(HttpContext context, IListService listService, int boardId, int listId)
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

async Task<IResult> RestoreList(HttpContext context, IListService listService, int boardId, int listId)
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

app.Run();

public partial class Program { }
