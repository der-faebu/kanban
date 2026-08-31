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

public class BoardTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _secondUserId = null!;

    public BoardTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        _secondUserId = Guid.NewGuid().ToString();

        await SeedTestUsers();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedTestUsers()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user1 = new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = $"user1_{_userId}@example.com" };
        var user2 = new ApplicationUser { Id = _secondUserId, UserName = $"user_{_secondUserId}", Email = $"user2_{_secondUserId}@example.com" };

        dbContext.Users.Add(user1);
        dbContext.Users.Add(user2);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBoard_WithValidData_ReturnsCreated()
    {
        var request = new CreateBoardRequest { Name = "Test Board", Description = "Test Description" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync("/api/boards", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(board);
        Assert.Equal("Test Board", board!.Name);
        Assert.Equal(_userId, board.OwnerId);
    }

    [Fact]
    public async Task CreateBoard_WithoutName_ReturnsBadRequest()
    {
        var request = new CreateBoardRequest { Name = "" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync("/api/boards", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetUserBoards_ReturnsOwnedAndMemberBoards()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board1 = new Board { Name = "Board 1", OwnerId = _userId, IsDeleted = false };
            var board2 = new Board { Name = "Board 2", OwnerId = _secondUserId, IsDeleted = false };
            dbContext.Boards.AddRange(board1, board2);
            await dbContext.SaveChangesAsync();

            var member = new BoardMember { BoardId = board2.Id, UserId = _userId, Role = BoardMemberRole.Member };
            dbContext.BoardMembers.Add(member);
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/api/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var boards = await response.Content.ReadFromJsonAsync<List<Board>>();
        Assert.NotNull(boards);
        Assert.Equal(2, boards!.Count);
    }

    [Fact]
    public async Task GetBoardById_WithAuthorizedUser_ReturnsBoard()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Test Board", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        var response = await _client.GetAsync($"/api/boards/{boardId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var retrievedBoard = await response.Content.ReadFromJsonAsync<Board>();
        Assert.NotNull(retrievedBoard);
        Assert.Equal("Test Board", retrievedBoard!.Name);
    }

    [Fact]
    public async Task DeleteBoard_ByOwner_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Test Board", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        var response = await _client.DeleteAsync($"/api/boards/{boardId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddBoardMember_ByOwner_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Test Board", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;
        }

        var request = new AddBoardMemberRequest { MemberId = _secondUserId, Role = BoardMemberRole.Member };
        var response = await _client.PostAsJsonAsync($"/api/boards/{boardId}/members", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoveBoardMember_ByOwner_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Test Board", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;

            var member = new BoardMember { BoardId = boardId, UserId = _secondUserId, Role = BoardMemberRole.Member };
            dbContext.BoardMembers.Add(member);
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.DeleteAsync($"/api/boards/{boardId}/members/{_secondUserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardMembers_ReturnsAllMembers()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int boardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var board = new Board { Name = "Test Board", OwnerId = _userId };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync();
            boardId = board.Id;

            var member = new BoardMember { BoardId = boardId, UserId = _secondUserId, Role = BoardMemberRole.Member };
            dbContext.BoardMembers.Add(member);
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/boards/{boardId}/members");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = await response.Content.ReadFromJsonAsync<List<BoardMember>>();
        Assert.NotNull(members);
        Assert.Single(members!);
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
