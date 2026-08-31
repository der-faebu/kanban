using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Kanban.Data;
using Kanban.Data.Entities;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class ChecklistTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public ChecklistTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        await SeedTestData();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedTestData()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = $"user_{_userId}@example.com" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var board = new Board { Name = "Test Board", OwnerId = _userId };
        dbContext.Boards.Add(board);
        await dbContext.SaveChangesAsync();
        _boardId = board.Id;

        var list = new List { BoardId = _boardId, Name = "Test List", Position = 0 };
        dbContext.Lists.Add(list);
        await dbContext.SaveChangesAsync();
        _listId = list.Id;

        var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
        dbContext.Cards.Add(card);
        await dbContext.SaveChangesAsync();
        _cardId = card.Id;
    }

    private async Task<string> CreateOtherUserAsync()
    {
        var otherUserId = Guid.NewGuid().ToString();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherUser = new ApplicationUser { Id = otherUserId, UserName = $"user_{otherUserId}", Email = $"user_{otherUserId}@example.com" };
        dbContext.Users.Add(otherUser);
        await dbContext.SaveChangesAsync();
        return otherUserId;
    }

    private void AuthenticateAs(string userId)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
    }

    [Fact]
    public async Task AddItem_WithValidText_ReturnsCreatedWithCorrectPosition()
    {
        AuthenticateAs(_userId);

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "First task" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<ChecklistItem>();
        Assert.NotNull(item);
        Assert.Equal("First task", item!.Text);
        Assert.Equal(0, item.Position);
        Assert.False(item.IsDone);
    }

    [Fact]
    public async Task AddItem_WithEmptyText_ReturnsBadRequest()
    {
        AuthenticateAs(_userId);

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddItem_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "sneaky" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetItems_ReturnsInPositionOrder()
    {
        AuthenticateAs(_userId);

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "first" });
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "second" });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<ChecklistItem>>();
        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Equal("first", items[0].Text);
        Assert.Equal("second", items[1].Text);
    }

    [Fact]
    public async Task GetItems_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateItemText_ChangesText()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "original" });
        var created = await createResponse.Content.ReadFromJsonAsync<ChecklistItem>();

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}", new UpdateChecklistItemRequest { Text = "edited" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await dbContext.ChecklistItems.FindAsync(created.Id);
        Assert.Equal("edited", item!.Text);
    }

    [Fact]
    public async Task ToggleItem_FlipsIsDone()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "task" });
        var created = await createResponse.Content.ReadFromJsonAsync<ChecklistItem>();

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}/toggle", new ToggleChecklistItemRequest { IsDone = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await dbContext.ChecklistItems.FindAsync(created.Id);
        Assert.True(item!.IsDone);
    }

    [Fact]
    public async Task DeleteItem_ReturnsNoContent()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "to delete" });
        var created = await createResponse.Content.ReadFromJsonAsync<ChecklistItem>();

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Null(await dbContext.ChecklistItems.FindAsync(created.Id));
    }

    [Fact]
    public async Task ReorderItems_PersistsNewPositions()
    {
        AuthenticateAs(_userId);
        var first = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "first" })).Content.ReadFromJsonAsync<ChecklistItem>();
        var second = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "second" })).Content.ReadFromJsonAsync<ChecklistItem>();

        var reorderRequest = new ReorderChecklistItemsRequest
        {
            Positions =
            [
                new ChecklistItemPosition { ItemId = first!.Id, Position = 1 },
                new ChecklistItemPosition { ItemId = second!.Id, Position = 0 }
            ]
        };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/reorder", reorderRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, (await dbContext.ChecklistItems.FindAsync(first.Id))!.Position);
        Assert.Equal(0, (await dbContext.ChecklistItems.FindAsync(second.Id))!.Position);
    }

    [Fact]
    public async Task UpdateItemText_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        var created = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "task" })).Content.ReadFromJsonAsync<ChecklistItem>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}", new UpdateChecklistItemRequest { Text = "hijacked" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ToggleItem_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        var created = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "task" })).Content.ReadFromJsonAsync<ChecklistItem>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}/toggle", new ToggleChecklistItemRequest { IsDone = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteItem_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        var created = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "task" })).Content.ReadFromJsonAsync<ChecklistItem>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/{created!.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReorderItems_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        var created = await (await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items", new CreateChecklistItemRequest { Text = "task" })).Content.ReadFromJsonAsync<ChecklistItem>();

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var reorderRequest = new ReorderChecklistItemsRequest
        {
            Positions = [new ChecklistItemPosition { ItemId = created!.Id, Position = 0 }]
        };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/checklist-items/reorder", reorderRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private string CreateToken(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var key = new System.Text.UTF8Encoding().GetBytes("test-secret-key-long-enough-for-hs256");
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(key),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
