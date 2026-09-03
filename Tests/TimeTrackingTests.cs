using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Kanban.Data;
using Kanban.Data.Entities;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class TimeTrackingTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public TimeTrackingTests()
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

    private async Task<string> CreateOtherUserAsync(bool asBoardMember = false)
    {
        var otherUserId = Guid.NewGuid().ToString();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var otherUser = new ApplicationUser { Id = otherUserId, UserName = $"user_{otherUserId}", Email = $"user_{otherUserId}@example.com" };
        dbContext.Users.Add(otherUser);
        await dbContext.SaveChangesAsync();

        if (asBoardMember)
        {
            dbContext.BoardMembers.Add(new BoardMember { BoardId = _boardId, UserId = otherUserId, Role = BoardMemberRole.Member });
            await dbContext.SaveChangesAsync();
        }

        return otherUserId;
    }

    private void AuthenticateAs(string userId)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
    }

    [Fact]
    public async Task SetEstimate_ReturnsOk_AndPersistsValue()
    {
        AuthenticateAs(_userId);

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/estimate", new SetCardEstimateRequest { EstimatedHours = 8.5m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}");
        var card = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal(8.5m, card!.EstimatedHours);
    }

    [Fact]
    public async Task SetEstimate_Clear_SetsValueToNull()
    {
        AuthenticateAs(_userId);
        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/estimate", new SetCardEstimateRequest { EstimatedHours = 5m });

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/estimate", new SetCardEstimateRequest { EstimatedHours = null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}");
        var card = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Null(card!.EstimatedHours);
    }

    [Fact]
    public async Task CreateTimeLogEntry_WithValidData_ReturnsCreated()
    {
        AuthenticateAs(_userId);

        var request = new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 2.5m, Note = "Investigated the bug" };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await response.Content.ReadFromJsonAsync<TimeLogEntry>();
        Assert.NotNull(entry);
        Assert.Equal(2.5m, entry!.DurationHours);
        Assert.Equal("Investigated the bug", entry.Note);
        Assert.Equal(_userId, entry.UserId);
    }

    [Fact]
    public async Task CreateTimeLogEntry_WithZeroDuration_ReturnsBadRequest()
    {
        AuthenticateAs(_userId);

        var request = new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 0 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateTimeLogEntry_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var request = new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTimeLogEntries_SumsToLoggedHours()
    {
        AuthenticateAs(_userId);
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 2m });
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1.25m });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries");
        var entries = await response.Content.ReadFromJsonAsync<List<TimeLogEntry>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, entries!.Count);
        Assert.Equal(3.25m, entries.Sum(e => e.DurationHours));
    }

    [Fact]
    public async Task GetCardLoggedHoursIncludingSubCards_SumsParentAndDirectChildren()
    {
        AuthenticateAs(_userId);

        int subCardId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subCard = new Card { ListId = _listId, ParentCardId = _cardId, Title = "Sub-card", Position = 1 };
            dbContext.Cards.Add(subCard);
            await dbContext.SaveChangesAsync();
            subCardId = subCard.Id;
        }

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 2m });
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{subCardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1.5m });

        using var scope2 = _factory.Services.CreateScope();
        var timeTrackingService = scope2.ServiceProvider.GetRequiredService<ITimeTrackingService>();

        var ownOnly = await timeTrackingService.GetCardLoggedHoursAsync(_cardId, _userId);
        var withSubCards = await timeTrackingService.GetCardLoggedHoursIncludingSubCardsAsync(_cardId, _userId);

        Assert.Equal(2m, ownOnly);
        Assert.Equal(3.5m, withSubCards);
    }

    [Fact]
    public async Task UpdateTimeLogEntry_AsAuthor_UpdatesFields()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1m, Note = "original" });
        var created = await createResponse.Content.ReadFromJsonAsync<TimeLogEntry>();

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries/{created!.Id}", new UpdateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 3m, Note = "edited" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = await dbContext.TimeLogEntries.FindAsync(created.Id);
        Assert.Equal(3m, entry!.DurationHours);
        Assert.Equal("edited", entry.Note);
        Assert.True(entry.UpdatedAt > entry.CreatedAt);
    }

    [Fact]
    public async Task UpdateTimeLogEntry_AsNonAuthor_IsForbidden()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1m });
        var created = await createResponse.Content.ReadFromJsonAsync<TimeLogEntry>();

        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries/{created!.Id}", new UpdateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 99m });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTimeLogEntry_AsAuthor_ReturnsNoContent()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1m });
        var created = await createResponse.Content.ReadFromJsonAsync<TimeLogEntry>();

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTimeLogEntry_AsBoardOwnerOnSomeoneElsesEntry_ReturnsNoContent()
    {
        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1m });
        var created = await createResponse.Content.ReadFromJsonAsync<TimeLogEntry>();

        AuthenticateAs(_userId); // board owner

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTimeLogEntry_AsNeitherAuthorNorOwner_IsForbidden()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries", new CreateTimeLogEntryRequest { Date = DateTime.UtcNow.Date, DurationHours = 1m });
        var created = await createResponse.Content.ReadFromJsonAsync<TimeLogEntry>();

        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/time-entries/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
