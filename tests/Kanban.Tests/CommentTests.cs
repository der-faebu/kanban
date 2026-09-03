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

public class CommentTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;
    private int _cardId;

    public CommentTests()
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
    public async Task CreateComment_WithValidText_ReturnsCreated()
    {
        AuthenticateAs(_userId);

        var request = new CreateCommentRequest { Text = "This looks good" };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = await response.Content.ReadFromJsonAsync<Comment>();
        Assert.NotNull(comment);
        Assert.Equal("This looks good", comment!.Text);
        Assert.Equal(_userId, comment.AuthorId);
    }

    [Fact]
    public async Task CreateComment_WithEmptyText_ReturnsBadRequest()
    {
        AuthenticateAs(_userId);

        var request = new CreateCommentRequest { Text = "   " };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var request = new CreateCommentRequest { Text = "sneaky comment" };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateComment_FreshComment_IsNotMarkedAsEdited()
    {
        AuthenticateAs(_userId);

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "first" });
        var comment = await response.Content.ReadFromJsonAsync<Comment>();

        Assert.Equal(comment!.CreatedAt, comment.UpdatedAt);
    }

    [Fact]
    public async Task GetComments_ReturnsInChronologicalOrder()
    {
        AuthenticateAs(_userId);

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "first" });
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "second" });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/comments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var comments = await response.Content.ReadFromJsonAsync<List<Comment>>();
        Assert.NotNull(comments);
        Assert.Equal(2, comments!.Count);
        Assert.Equal("first", comments[0].Text);
        Assert.Equal("second", comments[1].Text);
    }

    [Fact]
    public async Task GetComments_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{_cardId}/comments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateComment_AsAuthor_UpdatesTextAndTimestamp()
    {
        AuthenticateAs(_userId);

        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "original" });
        var created = await createResponse.Content.ReadFromJsonAsync<Comment>();

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments/{created!.Id}", new UpdateCommentRequest { Text = "edited" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var comment = await dbContext.Comments.FindAsync(created.Id);
        Assert.Equal("edited", comment!.Text);
        Assert.True(comment.UpdatedAt > comment.CreatedAt);
    }

    [Fact]
    public async Task UpdateComment_AsNonAuthor_IsForbidden()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "original" });
        var created = await createResponse.Content.ReadFromJsonAsync<Comment>();

        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments/{created!.Id}", new UpdateCommentRequest { Text = "hijacked" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_AsAuthor_ReturnsNoContent()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "to delete" });
        var created = await createResponse.Content.ReadFromJsonAsync<Comment>();

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/comments/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_AsBoardOwnerOnSomeoneElsesComment_ReturnsNoContent()
    {
        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "member comment" });
        var created = await createResponse.Content.ReadFromJsonAsync<Comment>();

        AuthenticateAs(_userId); // board owner

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/comments/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteComment_AsNeitherAuthorNorOwner_IsForbidden()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{_cardId}/comments", new CreateCommentRequest { Text = "owner comment" });
        var created = await createResponse.Content.ReadFromJsonAsync<Comment>();

        var otherUserId = await CreateOtherUserAsync(asBoardMember: true);
        AuthenticateAs(otherUserId);

        var response = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{_cardId}/comments/{created!.Id}");

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
