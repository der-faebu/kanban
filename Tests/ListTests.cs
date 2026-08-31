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

public class ListTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private string _secondUserId = null!;
    private int _boardId;

    public ListTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _userId = Guid.NewGuid().ToString();
        _secondUserId = Guid.NewGuid().ToString();

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
    }

    [Fact]
    public async Task CreateList_WithValidData_ReturnsCreated()
    {
        var request = new CreateListRequest { Name = "To Do" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync($"/api/boards/{_boardId}/lists", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List>();
        Assert.NotNull(list);
        Assert.Equal("To Do", list!.Name);
    }

    [Fact]
    public async Task CreateList_WithoutName_ReturnsBadRequest()
    {
        var request = new CreateListRequest { Name = "" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync($"/api/boards/{_boardId}/lists", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardLists_ReturnsAllLists()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Lists.AddRange(
                new List { BoardId = _boardId, Name = "To Do", Position = 0 },
                new List { BoardId = _boardId, Name = "In Progress", Position = 1 }
            );
            await dbContext.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/boards/{_boardId}/lists");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lists = await response.Content.ReadFromJsonAsync<List<List>>();
        Assert.NotNull(lists);
        Assert.Equal(2, lists!.Count);
    }

    [Fact]
    public async Task GetListById_ReturnsListDetails()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int listId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list = new List { BoardId = _boardId, Name = "Test List", Position = 0 };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            listId = list.Id;
        }

        var response = await _client.GetAsync($"/api/boards/{_boardId}/lists/{listId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var retrievedList = await response.Content.ReadFromJsonAsync<List>();
        Assert.NotNull(retrievedList);
        Assert.Equal("Test List", retrievedList!.Name);
    }

    [Fact]
    public async Task RenameList_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int listId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list = new List { BoardId = _boardId, Name = "Old Name", Position = 0 };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            listId = list.Id;
        }

        var request = new RenameListRequest { Name = "New Name" };
        var response = await _client.PutAsJsonAsync($"/api/boards/{_boardId}/lists/{listId}/name", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SoftDeleteList_ReturnsNoContent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int listId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list = new List { BoardId = _boardId, Name = "Test List", Position = 0 };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            listId = list.Id;
        }

        var response = await _client.DeleteAsync($"/api/boards/{_boardId}/lists/{listId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task RestoreList_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int listId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list = new List { BoardId = _boardId, Name = "Test List", Position = 0, IsDeleted = true };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            listId = list.Id;
        }

        var response = await _client.PostAsync($"/api/boards/{_boardId}/lists/{listId}/restore", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReorderLists_UpdatesPositions()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int list1Id = 0, list2Id = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var list1 = new List { BoardId = _boardId, Name = "List 1", Position = 0 };
            var list2 = new List { BoardId = _boardId, Name = "List 2", Position = 1 };
            dbContext.Lists.AddRange(list1, list2);
            await dbContext.SaveChangesAsync();
            list1Id = list1.Id;
            list2Id = list2.Id;
        }

        var request = new ReorderListsRequest
        {
            Positions = new List<(int, int)> { (list1Id, 1), (list2Id, 0) }
        };
        var response = await _client.PutAsJsonAsync($"/api/boards/{_boardId}/lists/reorder", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
