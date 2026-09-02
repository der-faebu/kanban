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

public class ProjectTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listId;

    public ProjectTests()
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

    private void AuthenticateAs(string userId)
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(userId));
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

    private async Task<int> CreateCardAsync(int listId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var card = new Card { ListId = listId, Title = "Test Card", Position = 0 };
        dbContext.Cards.Add(card);
        await dbContext.SaveChangesAsync();
        return card.Id;
    }

    [Fact]
    public async Task CreateProject_WithValidData_ReturnsCreated()
    {
        AuthenticateAs(_userId);

        var request = new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId };
        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<Project>();
        Assert.NotNull(project);
        Assert.Equal("Rewrite Initiative", project!.Name);
        Assert.Equal("#3B82F6", project.Color);
    }

    [Fact]
    public async Task CreateProject_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var request = new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId };
        var response = await _client.PostAsJsonAsync("/api/projects", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAllProjects_ReturnsCreatedProject()
    {
        AuthenticateAs(_userId);

        await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId });

        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<Project>>();
        Assert.NotNull(projects);
        Assert.Contains(projects!, p => p.Name == "Rewrite Initiative");
    }

    [Fact]
    public async Task AddProject_ToCard_ReturnsOk_AndCardProjects_ReflectsTag()
    {
        AuthenticateAs(_userId);

        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId });
        var project = await createResponse.Content.ReadFromJsonAsync<Project>();
        var cardId = await CreateCardAsync(_listId);

        var addResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/projects", new AddCardProjectRequest { ProjectId = project!.Id });
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}/projects");
        var cardProjects = await getResponse.Content.ReadFromJsonAsync<List<Project>>();
        Assert.Single(cardProjects!);
        Assert.Equal(project.Id, cardProjects![0].Id);
    }

    [Fact]
    public async Task RemoveProject_FromCard_ReturnsNoContent_AndCardProjects_ReflectsUntag()
    {
        AuthenticateAs(_userId);

        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId });
        var project = await createResponse.Content.ReadFromJsonAsync<Project>();
        var cardId = await CreateCardAsync(_listId);
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/projects", new AddCardProjectRequest { ProjectId = project!.Id });

        var removeResponse = await _client.DeleteAsync($"/api/lists/{_listId}/cards/{cardId}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}/projects");
        var cardProjects = await getResponse.Content.ReadFromJsonAsync<List<Project>>();
        Assert.Empty(cardProjects!);
    }

    [Fact]
    public async Task ProjectCreatedOnOneBoard_IsVisibleAndUsableOnAnotherBoard()
    {
        AuthenticateAs(_userId);

        // Create the project while looking at board A's card.
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Cross-Board Initiative", Color = "#EAB308", BoardId = _boardId });
        var project = await createResponse.Content.ReadFromJsonAsync<Project>();

        // A second, unrelated board owned by the same user.
        int otherBoardId;
        int otherListId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var otherBoard = new Board { Name = "Other Board", OwnerId = _userId };
            dbContext.Boards.Add(otherBoard);
            await dbContext.SaveChangesAsync();
            otherBoardId = otherBoard.Id;

            var otherList = new List { BoardId = otherBoardId, Name = "Other List", Position = 0 };
            dbContext.Lists.Add(otherList);
            await dbContext.SaveChangesAsync();
            otherListId = otherList.Id;
        }

        // The project pool returned to a client viewing the unrelated board includes it.
        var listResponse = await _client.GetAsync("/api/projects");
        var allProjects = await listResponse.Content.ReadFromJsonAsync<List<Project>>();
        Assert.Contains(allProjects!, p => p.Id == project!.Id);

        // And it can be tagged onto a card that lives on the unrelated board.
        var otherCardId = await CreateCardAsync(otherListId);
        var tagResponse = await _client.PostAsJsonAsync($"/api/lists/{otherListId}/cards/{otherCardId}/projects", new AddCardProjectRequest { ProjectId = project!.Id });
        Assert.Equal(HttpStatusCode.OK, tagResponse.StatusCode);

        var cardProjectsResponse = await _client.GetAsync($"/api/lists/{otherListId}/cards/{otherCardId}/projects");
        var taggedProjects = await cardProjectsResponse.Content.ReadFromJsonAsync<List<Project>>();
        Assert.Single(taggedProjects!, p => p.Id == project.Id);
    }

    [Fact]
    public async Task AddProject_AsNonBoardMember_IsRejected()
    {
        AuthenticateAs(_userId);
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new CreateProjectRequest { Name = "Rewrite Initiative", Color = "#3B82F6", BoardId = _boardId });
        var project = await createResponse.Content.ReadFromJsonAsync<Project>();
        var cardId = await CreateCardAsync(_listId);

        var otherUserId = await CreateOtherUserAsync();
        AuthenticateAs(otherUserId);

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/projects", new AddCardProjectRequest { ProjectId = project!.Id });

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
