using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Kanban.Components.Board;
using Kanban.Data;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

// SortableJS's own drag mechanics are pure client-side JS with no browser automation in this
// repo (matches the accepted gap for panning/list-drag), but BoardView.OnCardDropped -- the
// [JSInvokable] entry point a real drag would call into -- is plain C# orchestrating
// CardService.MoveCardAsync/ReorderCardsAsync. Invoking it directly here, bypassing SortableJS
// entirely, proves that orchestration without needing a live drag.
public class BoardViewCardDropTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private KanbanWebApplicationFactory _factory = null!;
    private IServiceScope _scope = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listAId;
    private int _listBId;

    async Task IAsyncLifetime.InitializeAsync()
    {
        _factory = new KanbanWebApplicationFactory();
        _ = _factory.Server;
        _scope = _factory.Services.CreateScope();

        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;

        _userId = Guid.NewGuid().ToString();
        var dbContext = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = $"user_{_userId}@example.com" });
        await dbContext.SaveChangesAsync();

        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthenticationStateProvider(_userId));
        RegisterScopedService<IBoardService>();
        RegisterScopedService<IListService>();
        RegisterScopedService<ICardService>();
        RegisterScopedService<IJwtTokenService>();
        Services.AddScoped<DragDropState>();

        var boardService = _scope.ServiceProvider.GetRequiredService<IBoardService>();
        var listService = _scope.ServiceProvider.GetRequiredService<IListService>();
        var cardService = _scope.ServiceProvider.GetRequiredService<ICardService>();

        var board = await boardService.CreateBoardAsync(_userId, "Drag test board", "");
        _boardId = board.Id;
        var listA = await listService.CreateListAsync(_boardId, _userId, "List A");
        var listB = await listService.CreateListAsync(_boardId, _userId, "List B");
        _listAId = listA.Id;
        _listBId = listB.Id;

        await cardService.CreateCardAsync(_listAId, _userId, "Card 1", "");
        await cardService.CreateCardAsync(_listAId, _userId, "Card 2", "");
        await cardService.CreateCardAsync(_listAId, _userId, "Card 3", "");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _factory.DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task OnCardDropped_SameList_ReordersToMatchDroppedOrder()
    {
        var cardIds = await GetOrderedCardIdsAsync(_listAId);
        // Simulate dragging card[0] to the end: [1, 2, 0].
        var newOrder = new[] { cardIds[1], cardIds[2], cardIds[0] };

        var component = await RenderBoardAsync();
        await component.InvokeAsync(() => component.Instance.OnCardDropped(cardIds[0], _listAId, _listAId, newOrder));

        var resultingOrder = await GetOrderedCardIdsAsync(_listAId);
        Assert.Equal(newOrder, resultingOrder);
    }

    [Fact]
    public async Task OnCardDropped_CrossList_MovesCardAndReordersDestination()
    {
        var cardIds = await GetOrderedCardIdsAsync(_listAId);
        var movedCardId = cardIds[1];
        // Card 2 dropped as the first (only) card in the previously-empty List B.
        var newOrderInB = new[] { movedCardId };

        var component = await RenderBoardAsync();
        await component.InvokeAsync(() => component.Instance.OnCardDropped(movedCardId, _listAId, _listBId, newOrderInB));

        var cardService = _scope.ServiceProvider.GetRequiredService<ICardService>();
        var movedCard = await cardService.GetCardByIdAsync(movedCardId, _userId);

        Assert.NotNull(movedCard);
        Assert.Equal(_listBId, movedCard!.ListId);
        Assert.Equal(0, movedCard.Position);

        var remainingInA = await GetOrderedCardIdsAsync(_listAId);
        Assert.Equal([cardIds[0], cardIds[2]], remainingInA);
    }

    private async Task<int[]> GetOrderedCardIdsAsync(int listId)
    {
        var cardService = _scope.ServiceProvider.GetRequiredService<ICardService>();
        var cards = await cardService.GetListCardsAsync(listId, _userId);
        return cards.OrderBy(c => c.Position).Select(c => c.Id).ToArray();
    }

    private async Task<IRenderedComponent<BoardView>> RenderBoardAsync()
    {
        var component = Render<BoardView>(parameters => parameters.Add(p => p.BoardId, _boardId));
        component.WaitForState(() => component.Markup.Contains("List A"), WaitTimeout);
        return component;
    }

    private void RegisterScopedService<TService>() where TService : class =>
        Services.AddSingleton(_scope.ServiceProvider.GetRequiredService<TService>());
}
