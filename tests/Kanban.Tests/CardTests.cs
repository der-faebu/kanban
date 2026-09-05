using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
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
    public async Task MoveCard_IntoListWithStateMapping_SetsCardState()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        int targetListId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, State = CardState.NotStarted };
            dbContext.Cards.Add(card);
            var list = new List { BoardId = _boardId, Name = "Done List", Position = 1, AssociatedState = CardState.Done };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
            targetListId = list.Id;
        }

        var request = new MoveCardRequest { TargetListId = targetListId, Position = 0 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/move", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movedCard = await assertContext.Cards.FirstAsync(c => c.Id == cardId);
        Assert.Equal(CardState.Done, movedCard.State);
        Assert.True(movedCard.StateSetAutomatically);
    }

    [Fact]
    public async Task MoveCard_IntoListWithoutStateMapping_LeavesCardStateUnchanged()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        int targetListId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, State = CardState.InProgress };
            dbContext.Cards.Add(card);
            var list = new List { BoardId = _boardId, Name = "Unmapped List", Position = 1 };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
            targetListId = list.Id;
        }

        var request = new MoveCardRequest { TargetListId = targetListId, Position = 0 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/move", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movedCard = await assertContext.Cards.FirstAsync(c => c.Id == cardId);
        Assert.Equal(CardState.InProgress, movedCard.State);
    }

    [Fact]
    public async Task MoveCard_IntoListWithStateMapping_OverridesManuallySetState()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int cardId = 0;
        int targetListId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Test Card", Position = 0, State = CardState.Done, StateSetAutomatically = false };
            dbContext.Cards.Add(card);
            var list = new List { BoardId = _boardId, Name = "Not Started List", Position = 1, AssociatedState = CardState.NotStarted };
            dbContext.Lists.Add(list);
            await dbContext.SaveChangesAsync();
            cardId = card.Id;
            targetListId = list.Id;
        }

        var request = new MoveCardRequest { TargetListId = targetListId, Position = 0 };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{cardId}/move", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var movedCard = await assertContext.Cards.FirstAsync(c => c.Id == cardId);
        Assert.Equal(CardState.NotStarted, movedCard.State);
        Assert.True(movedCard.StateSetAutomatically);
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
    public async Task UpdateLabel_ReturnsOk_AndPersistsChanges()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int labelId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Old Name", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;
        }

        var request = new UpdateLabelRequest { Name = "New Name", Color = "#00FF00" };
        var response = await _client.PutAsJsonAsync($"/api/boards/{_boardId}/labels/{labelId}", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/boards/{_boardId}/labels");
        var labels = await getResponse.Content.ReadFromJsonAsync<List<Label>>();
        var updated = Assert.Single(labels!, l => l.Id == labelId);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal("#00FF00", updated.Color);
    }

    [Fact]
    public async Task UpdateLabel_ForNonexistentLabel_ReturnsNotFound()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var response = await _client.PutAsJsonAsync($"/api/boards/{_boardId}/labels/999999", new UpdateLabelRequest { Name = "Anything", Color = "#FF0000" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLabel_AsNonBoardMember_IsRejected()
    {
        int labelId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var label = new Label { BoardId = _boardId, Name = "Bug", Color = "#FF0000" };
            dbContext.Labels.Add(label);
            await dbContext.SaveChangesAsync();
            labelId = label.Id;
        }

        var otherUserId = await CreateOtherUserAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var response = await _client.PutAsJsonAsync($"/api/boards/{_boardId}/labels/{labelId}", new UpdateLabelRequest { Name = "Hijacked", Color = "#000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    [Fact]
    public async Task CreateSubCard_ReturnsCreated_AndSetsParentCardId()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int parentCardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Parent Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            parentCardId = card.Id;
        }

        var request = new CreateCardRequest { Title = "Sub-card" };
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var subCard = await response.Content.ReadFromJsonAsync<Card>();
        Assert.NotNull(subCard);
        Assert.Equal(parentCardId, subCard!.ParentCardId);
        Assert.Equal(_listId, subCard.ListId);
    }

    [Fact]
    public async Task GetSubCards_ReturnsChildrenOfParent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int parentCardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Parent Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            parentCardId = card.Id;
        }

        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Sub-card 1" });
        await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Sub-card 2" });

        var response = await _client.GetAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards");
        var subCards = await response.Content.ReadFromJsonAsync<List<Card>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, subCards!.Count);
        Assert.All(subCards, c => Assert.Equal(parentCardId, c.ParentCardId));
    }

    [Fact]
    public async Task CreateSubCard_UnderACardThatIsItselfASubCard_IsRejected()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        int parentCardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Parent Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            parentCardId = card.Id;
        }

        var childResponse = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Child card" });
        var childCard = await childResponse.Content.ReadFromJsonAsync<Card>();

        // childCard already has a ParentCardId -- giving it children (i.e. nesting a
        // grandchild under it) must be rejected, since a card can be a parent or a
        // child but never both.
        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{childCard!.Id}/subcards", new CreateCardRequest { Title = "Grandchild card" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateSubCard_AsNonBoardMember_IsRejected()
    {
        int parentCardId = 0;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Parent Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            parentCardId = card.Id;
        }

        var otherUserId = await CreateOtherUserAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(otherUserId));

        var response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Sub-card" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetCardState_LastChildBecomesDone_AutoClosesParent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var (parentCardId, child1Id, child2Id) = await SeedParentWithTwoChildrenAsync();

        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child1Id}/state", new SetCardStateRequest { State = CardState.Done });
        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child2Id}/state", new SetCardStateRequest { State = CardState.Done });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await dbContext.Cards.FirstAsync(c => c.Id == parentCardId);
        Assert.Equal(CardState.Done, parent.State);
        Assert.True(parent.StateSetAutomatically);
    }

    [Fact]
    public async Task SetCardState_ChildLeavesDone_AutoReopensAutoClosedParent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var (parentCardId, child1Id, child2Id) = await SeedParentWithTwoChildrenAsync();
        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child1Id}/state", new SetCardStateRequest { State = CardState.Done });
        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child2Id}/state", new SetCardStateRequest { State = CardState.Done });

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child1Id}/state", new SetCardStateRequest { State = CardState.InProgress });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await dbContext.Cards.FirstAsync(c => c.Id == parentCardId);
        Assert.Equal(CardState.InProgress, parent.State);
        Assert.True(parent.StateSetAutomatically);
    }

    [Fact]
    public async Task SetCardState_ChildLeavesDone_DoesNotReopenManuallyClosedParent()
    {
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken(_userId));

        var (parentCardId, child1Id, _) = await SeedParentWithTwoChildrenAsync();

        // Parent is manually marked Done while a child is still open.
        await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/state", new SetCardStateRequest { State = CardState.Done });

        var response = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child1Id}/state", new SetCardStateRequest { State = CardState.Done });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Then a child changes state again -- the manually-set parent must stay untouched.
        var response2 = await _client.PutAsJsonAsync($"/api/lists/{_listId}/cards/{child1Id}/state", new SetCardStateRequest { State = CardState.InProgress });
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parent = await dbContext.Cards.FirstAsync(c => c.Id == parentCardId);
        Assert.Equal(CardState.Done, parent.State);
        Assert.False(parent.StateSetAutomatically);
    }

    private async Task<(int parentCardId, int child1Id, int child2Id)> SeedParentWithTwoChildrenAsync()
    {
        int parentCardId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var card = new Card { ListId = _listId, Title = "Parent Card", Position = 0 };
            dbContext.Cards.Add(card);
            await dbContext.SaveChangesAsync();
            parentCardId = card.Id;
        }

        var child1Response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Child 1" });
        var child1 = await child1Response.Content.ReadFromJsonAsync<Card>();
        var child2Response = await _client.PostAsJsonAsync($"/api/lists/{_listId}/cards/{parentCardId}/subcards", new CreateCardRequest { Title = "Child 2" });
        var child2 = await child2Response.Content.ReadFromJsonAsync<Card>();

        return (parentCardId, child1!.Id, child2!.Id);
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
