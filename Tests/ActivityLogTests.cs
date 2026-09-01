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

public class ActivityLogTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public ActivityLogTests()
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
    public async Task CreatingCard_ProducesCardCreatedEntry()
    {
        AuthenticateAs(_userId);

        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards", new CreateCardRequest { Title = "New Card" });
        var card = await createResponse.Content.ReadFromJsonAsync<Card>();

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{card!.Id}/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();
        Assert.NotNull(activity);
        Assert.Single(activity!);
        Assert.Equal(ActivityType.CardCreated, activity[0].ActivityType);
        Assert.Equal(_userId, activity[0].UserId);
    }

    [Fact]
    public async Task UpdatingCard_ProducesCardUpdatedEntry()
    {
        AuthenticateAs(_userId);

        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}", new UpdateCardRequest { Title = "Renamed" });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.Contains(activity!, a => a.ActivityType == ActivityType.CardUpdated);
    }

    [Fact]
    public async Task MovingCard_ProducesCardMovedEntryWithSourceAndTargetListMetadata()
    {
        AuthenticateAs(_userId);

        int secondListId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var secondList = new List { BoardId = _boardId, Name = "Second List", Position = 1 };
            dbContext.Lists.Add(secondList);
            await dbContext.SaveChangesAsync();
            secondListId = secondList.Id;
        }

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/move", new MoveCardRequest { TargetListId = secondListId, Position = 0 });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        var moveEntry = Assert.Single(activity!, a => a.ActivityType == ActivityType.CardMoved);
        Assert.Contains($"\"sourceListId\":{_listId}", moveEntry.Metadata);
        Assert.Contains($"\"targetListId\":{secondListId}", moveEntry.Metadata);
    }

    [Fact]
    public async Task SettingCardState_ProducesStateChangedEntry()
    {
        AuthenticateAs(_userId);

        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/state", new SetCardStateRequest { State = CardState.InProgress });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        var entry = Assert.Single(activity!, a => a.ActivityType == ActivityType.StateChanged);
        Assert.Contains("\"state\":\"InProgress\"", entry.Metadata);
        Assert.Equal(_userId, entry.UserId);
    }

    [Fact]
    public async Task AddingLabel_ProducesLabelAddedEntry()
    {
        AuthenticateAs(_userId);

        int labelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;
        }

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/labels", new AddCardLabelRequest { LabelId = labelId });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.Contains(activity!, a => a.ActivityType == ActivityType.LabelAdded);
    }

    [Fact]
    public async Task AddingAssignee_ProducesAssigneeAddedEntry()
    {
        AuthenticateAs(_userId);
        var assigneeUserId = await CreateOtherUserAsync();

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/assignees", new AddCardAssigneeRequest { UserId = assigneeUserId });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.Contains(activity!, a => a.ActivityType == ActivityType.AssigneeAdded);
    }

    [Fact]
    public async Task RemovingLabel_ProducesLabelRemovedEntry()
    {
        AuthenticateAs(_userId);

        int labelId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;
        }

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/labels", new AddCardLabelRequest { LabelId = labelId });
        await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/labels/{labelId}");

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.Contains(activity!, a => a.ActivityType == ActivityType.LabelRemoved);
    }

    [Fact]
    public async Task RemovingAssignee_ProducesAssigneeRemovedEntry()
    {
        AuthenticateAs(_userId);
        var assigneeUserId = await CreateOtherUserAsync();

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/assignees", new AddCardAssigneeRequest { UserId = assigneeUserId });
        await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/assignees/{assigneeUserId}");

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.Contains(activity!, a => a.ActivityType == ActivityType.AssigneeRemoved);
    }

    [Fact]
    public async Task GetActivity_ReturnsMostRecentFirst()
    {
        AuthenticateAs(_userId);

        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}", new UpdateCardRequest { Title = "First update" });
        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}", new UpdateCardRequest { Title = "Second update" });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");
        var activity = await response.Content.ReadFromJsonAsync<List<CardActivity>>();

        Assert.True(activity!.Count >= 2);
        for (var i = 0; i < activity.Count - 1; i++)
        {
            Assert.True(activity[i].CreatedAt >= activity[i + 1].CreatedAt);
        }
    }

    [Fact]
    public async Task GetActivity_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/activity");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
