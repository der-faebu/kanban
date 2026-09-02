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
    public async Task CreateCard_DefaultsToNotStartedState()
    {
        var request = new CreateCardRequest { Title = "Test Card" };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards", request);

        var card = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(card);
        Assert.Equal(CardState.NotStarted, card!.State);
    }

    [Fact]
    public async Task SetCardState_ReturnsOk_AndPersistsState()
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

        var request = new SetCardStateRequest { State = CardState.InProgress };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/state", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal(CardState.InProgress, card2!.State);
    }

    [Fact]
    public async Task SetCardState_ToDone_ReturnsOk()
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

        var request = new SetCardStateRequest { State = CardState.Done };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/state", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal(CardState.Done, card2!.State);
    }

    [Fact]
    public async Task SetCardState_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var request = new SetCardStateRequest { State = CardState.InProgress };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/state", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetCardType_ReturnsOk_AndPersistsType()
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

        var request = new SetCardTypeRequest { Type = CardType.Bug };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/type", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal(CardType.Bug, card2!.Type);
    }

    [Fact]
    public async Task SetCardType_Clear_SetsTypeToNull()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, Type = CardType.Feature };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new SetCardTypeRequest { Type = null };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/type", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Null(card2!.Type);
    }

    [Fact]
    public async Task SetCardType_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var request = new SetCardTypeRequest { Type = CardType.Chore };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/type", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task UpdateCard_WithDueDate_ReturnsOk()
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

        var dueDate = DateTime.UtcNow.AddDays(5);
        var request = new UpdateCardRequest { Title = "Updated", DueDate = dueDate };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddAssignee_WithValidUserId_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        string assigneeUserId = Guid.NewGuid().ToString();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assignee = new ApplicationUser { Id = assigneeUserId, UserName = $"assignee_{assigneeUserId}", Email = $"assignee_{assigneeUserId}@example.com" };
            dbContext.Users.Add(assignee);
            await dbContext.SaveChangesAsync();

            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new AddCardAssigneeRequest { UserId = assigneeUserId };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/assignees", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAssignees_ReturnsEmptyList()
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

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}/assignees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignees = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(assignees);
        Assert.Empty(assignees!);
    }

    [Fact]
    public async Task CreateLabel_WithValidData_ReturnsCreated()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var request = new CreateLabelRequest { Name = "Bug", Color = "#FF0000" };
        var response = await _client.PostAsJsonAsync($"/api/boards/{_boardId}/labels", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetBoardLabels_ReturnsEmptyList()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.GetAsync($"/api/boards/{_boardId}/labels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var labels = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(labels);
        Assert.Empty(labels!);
    }

    [Fact]
    public async Task AddLabel_ToCard_ReturnsOk()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        int labelId = 0;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;

            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new AddCardLabelRequest { LabelId = labelId };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/labels", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCardLabels_ReturnsEmptyList()
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

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}/labels");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var labels = await response.Content.ReadFromJsonAsync<List<object>>();
        Assert.NotNull(labels);
        Assert.Empty(labels!);
    }

    [Fact]
    public async Task GetCardById_ForNonexistentCard_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCardById_ForDeletedCard_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, IsDeleted = true };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetDevReferenceUrl_ReturnsOk_AndPersistsUrl()
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

        var request = new SetCardDevReferenceUrlRequest { Url = "https://github.com/acme/repo/pull/42" };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/dev-link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal("https://github.com/acme/repo/pull/42", card2!.DevReferenceUrl);
    }

    [Fact]
    public async Task SetDevReferenceUrl_Clear_SetsUrlToNull()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, DevReferenceUrl = "https://github.com/acme/repo/pull/42" };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new SetCardDevReferenceUrlRequest { Url = null };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/dev-link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Null(card2!.DevReferenceUrl);
    }

    [Fact]
    public async Task SetTicketUrl_ReturnsOk_AndPersistsUrl()
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

        var request = new SetCardTicketUrlRequest { Url = "https://acme.atlassian.net/browse/PROJ-123" };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/ticket-link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Equal("https://acme.atlassian.net/browse/PROJ-123", card2!.TicketUrl);
    }

    [Fact]
    public async Task SetTicketUrl_Clear_SetsUrlToNull()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, TicketUrl = "https://acme.atlassian.net/browse/PROJ-123" };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        var request = new SetCardTicketUrlRequest { Url = "" };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/ticket-link", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");
        var card2 = await getResponse.Content.ReadFromJsonAsync<Card>();
        Assert.Null(card2!.TicketUrl);
    }

    [Fact]
    public async Task SetDevReferenceUrl_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var request = new SetCardDevReferenceUrlRequest { Url = "https://github.com/acme/repo/pull/42" };
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/dev-link", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task GetCardById_AsNonBoardMember_ReturnsNotFound()
    {
        var otherUserId = await CreateOtherUserAsync();

        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{cardId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddLabel_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        int cardId = 0;
        int labelId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;

            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var request = new AddCardLabelRequest { LabelId = labelId };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/labels", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddAssignee_AsNonBoardMember_IsRejected()
    {
        var otherUserId = await CreateOtherUserAsync();
        var assigneeUserId = Guid.NewGuid().ToString();
        int cardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var assignee = new ApplicationUser { Id = assigneeUserId, UserName = $"assignee_{assigneeUserId}", Email = $"assignee_{assigneeUserId}@example.com" };
            dbContext.Users.Add(assignee);
            await dbContext.SaveChangesAsync();

            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
        }

        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var request = new AddCardAssigneeRequest { UserId = assigneeUserId };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/assignees", request);

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
