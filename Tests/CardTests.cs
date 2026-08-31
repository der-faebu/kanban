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

public class CardTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;

    public CardTests()
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
    }

    [Fact]
    public async Task CreateCard_WithValidData_ReturnsCreated()
    {
        var request = new CreateCardRequest { Title = "Test Card" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        Assert.Equal("Test Card", card!.Title);
    }

    [Fact]
    public async Task GetListCards_ReturnsAllCards()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Cards.AddRange(
                new Card { ListId = _listId, Title = "Card 1", Position = 0 },
                new Card { ListId = _listId, Title = "Card 2", Position = 1 }
            );
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<Card>>();
        Assert.NotNull(cards);
        Assert.Equal(2, cards!.Count);
    }

    [Fact]
    public async Task UpdateCard_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Old Title", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new UpdateCardRequest { Title = "New Title" };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MoveCard_ToAnotherList_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        int secondListId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;

            var list = new List { BoardId = _boardId, Name = "Second List", Position = 1 };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            secondListId = list.Id;
        }

        var request = new MoveCardRequest { TargetListId = secondListId, Position = 0 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/move", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SoftDeleteCard_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{cardId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
