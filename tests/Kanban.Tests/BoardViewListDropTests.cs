using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Kanban.Components.Board;
using Kanban.Data;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

// Mirrors BoardViewCardDropTests: SortableJS's own drag mechanics are pure client-side JS with
// no browser automation in this repo, but BoardView.OnListDropped -- the [JSInvokable] entry
// point a real column drag would call into -- is plain C# orchestrating
// ListService.ReorderListsAsync. Invoking it directly here, bypassing SortableJS entirely,
// proves that orchestration without needing a live drag.
public class BoardViewListDropTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private KanbanWebApplicationFactory _factory = null!;
    private IServiceScope _scope = null!;
    private string _userId = null!;
    private int _boardId;
    private int _listAId;
    private int _listBId;
    private int _listCId;

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

        var boardService = _scope.ServiceProvider.GetRequiredService<IBoardService>();
        var listService = _scope.ServiceProvider.GetRequiredService<IListService>();

        var board = await boardService.CreateBoardAsync(_userId, "List drag test board", "");
        _boardId = board.Id;
        var listA = await listService.CreateListAsync(_boardId, _userId, "List A");
        var listB = await listService.CreateListAsync(_boardId, _userId, "List B");
        var listC = await listService.CreateListAsync(_boardId, _userId, "List C");
        _listAId = listA.Id;
        _listBId = listB.Id;
        _listCId = listC.Id;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _factory.DisposeAsync();
        await base.DisposeAsync();
    }

    [Fact]
    public async Task OnListDropped_ReordersListsToMatchDroppedOrder()
    {
        // Simulate dragging List A to the end: [B, C, A].
        var newOrder = new[] { _listBId, _listCId, _listAId };

        var component = await RenderBoardAsync();
        await component.InvokeAsync(() => component.Instance.OnListDropped(newOrder));

        var listService = _scope.ServiceProvider.GetRequiredService<IListService>();
        var lists = await listService.GetBoardListsAsync(_boardId, _userId);
        var resultingOrder = lists.OrderBy(l => l.Position).Select(l => l.Id).ToArray();

        Assert.Equal(newOrder, resultingOrder);
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
