using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Kanban.Data;
using Kanban.Data.Dtos;
using Kanban.Data.Entities;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class TrelloImportTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;

    public TrelloImportTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        await SeedTestUser();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedTestUser()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = $"user_{_userId}",
            Email = $"test_{_userId}@example.com"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ImportTrello_WithValidJson_CreatesBoard()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Import Test", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        var trelloJson = CreateValidTrelloJson();
        using var content = new MultipartFormDataContent();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trelloJson));
        content.Add(new StreamContent(stream), "file", "export.json");

        var response = await _client.PostAsync($"/api/import/trello/{boardId}", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TrelloImportResult>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(2, result.ListsImported);
        Assert.Equal(3, result.CardsImported);
    }

    [Fact]
    public async Task ImportTrello_WithoutFile_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Import Test", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        using var content = new MultipartFormDataContent();
        var response = await _client.PostAsync($"/api/import/trello/{boardId}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTrello_ByNonOwner_ReturnsForbidden()
    {
        var otherUserId = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                Id = otherUserId,
                UserName = $"user_{otherUserId}",
                Email = $"other_{otherUserId}@example.com"
            };
            dbContext.Users.Add(user);

            var board = new Board { Name = "Someone Else's Board", OwnerId = otherUserId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();

            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

            var trelloJson = CreateValidTrelloJson();
            using var content = new MultipartFormDataContent();
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trelloJson));
            content.Add(new StreamContent(stream), "file", "export.json");

            var response = await _client.PostAsync($"/api/import/trello/{board.Id}", content);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ImportTrello_WithInvalidJson_ReturnsBadRequest()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Import Test", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        using var content = new MultipartFormDataContent();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("invalid json {"));
        content.Add(new StreamContent(stream), "file", "export.json");

        var response = await _client.PostAsync($"/api/import/trello/{boardId}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImportTrello_WithNonExistentBoard_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var trelloJson = CreateValidTrelloJson();
        using var content = new MultipartFormDataContent();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trelloJson));
        content.Add(new StreamContent(stream), "file", "export.json");

        var response = await _client.PostAsync($"/api/import/trello/9999", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ImportTrello_WithAssignees_MapsUsersCorrectly()
    {
        var assigneeUserId = Guid.NewGuid().ToString();
        var assigneeEmail = "assignee@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assigneeUser = new ApplicationUser
            {
                Id = assigneeUserId,
                UserName = "assignee_user",
                Email = assigneeEmail
            };
            dbContext.Users.Add(assigneeUser);
            await dbContext.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Import Test", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        var trelloJson = CreateTrelloJsonWithAssignee(assigneeEmail);
        using var content = new MultipartFormDataContent();
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trelloJson));
        content.Add(new StreamContent(stream), "file", "export.json");

        var response = await _client.PostAsync($"/api/import/trello/{boardId}", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cards = await dbContext.Cards.ToListAsync();
            var cardAssignees = await dbContext.CardAssignees.ToListAsync();

            Assert.Single(cards);
            Assert.Single(cardAssignees);
            Assert.Equal(assigneeUserId, cardAssignees.First().UserId);
        }
    }

    private string CreateValidTrelloJson()
    {
        var trelloBoard = new
        {
            id = "board123",
            name = "Test Board",
            desc = "Test Description",
            lists = new[]
            {
                new { id = "list1", name = "To Do", pos = 0 },
                new { id = "list2", name = "Done", pos = 1 }
            },
            cards = new[]
            {
                new { id = "card1", name = "Card 1", desc = "Description 1", idList = "list1", pos = 0, due = (DateTime?)null, idMembers = new string[0], idLabels = new string[0] },
                new { id = "card2", name = "Card 2", desc = "Description 2", idList = "list1", pos = 1, due = (DateTime?)null, idMembers = new string[0], idLabels = new string[0] },
                new { id = "card3", name = "Card 3", desc = "Description 3", idList = "list2", pos = 0, due = (DateTime?)null, idMembers = new string[0], idLabels = new string[0] }
            },
            labels = new object[0],
            members = new object[0]
        };

        return JsonSerializer.Serialize(trelloBoard);
    }

    private string CreateTrelloJsonWithAssignee(string email)
    {
        var trelloBoard = new
        {
            id = "board123",
            name = "Test Board",
            desc = "Test Description",
            lists = new[]
            {
                new { id = "list1", name = "To Do", pos = 0 }
            },
            cards = new[]
            {
                new { id = "card1", name = "Card with Assignee", desc = "Description", idList = "list1", pos = 0, due = (DateTime?)null, idMembers = new[] { "member1" }, idLabels = new string[0] }
            },
            labels = new object[0],
            members = new[]
            {
                new { id = "member1", email = email, fullName = "Test User" }
            }
        };

        return JsonSerializer.Serialize(trelloBoard);
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
