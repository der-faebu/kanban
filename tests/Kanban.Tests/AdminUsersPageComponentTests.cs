using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Kanban.Components.Pages.Admin;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

// bUnit coverage for the one thing the HTTP-level AdminUsersPageTests can't prove: that typing
// into the search box actually re-filters the rendered rows (MudTable's Filter prop runs
// client-side, so a server-rendered HTML snapshot only ever shows the unfiltered initial list).
public class AdminUsersPageComponentTests : BunitContext, IAsyncLifetime
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    private KanbanWebApplicationFactory _factory = null!;
    private IServiceScope _scope = null!;

    async Task IAsyncLifetime.InitializeAsync()
    {
        _factory = new KanbanWebApplicationFactory();
        // Accessing Server forces WebApplicationFactory to build its host (and start the
        // Testcontainers Postgres container) before we pull services out of it.
        _ = _factory.Server;
        _scope = _factory.Services.CreateScope();

        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_scope.ServiceProvider.GetRequiredService<IAdminUserService>());

        var client = _factory.CreateClient();
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(client, _factory.Services, "alice@example.com", "TestPassword123!");
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(client, _factory.Services, "bob@example.com", "TestPassword123!");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope.Dispose();
        await _factory.DisposeAsync();
        // Async, not the sync Dispose(): MudBlazor's PopoverService only implements
        // IAsyncDisposable, and bUnit's synchronous Dispose() throws trying to dispose it.
        await base.DisposeAsync();
    }

    [Fact]
    public async Task SearchBox_FiltersRowsAsYouType()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        var searchInput = component.Find("input");
        await component.InvokeAsync(() => searchInput.Input("alice"));

        component.WaitForState(() => component.FindAll("tbody tr").Count == 1, WaitTimeout);
        Assert.Contains("alice@example.com", component.Markup);
        Assert.DoesNotContain("bob@example.com", component.Markup);
    }

    [Fact]
    public async Task SearchBox_ClearedAfterFiltering_ShowsAllRowsAgain()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        var searchInput = component.Find("input");
        await component.InvokeAsync(() => searchInput.Input("alice"));
        component.WaitForState(() => component.FindAll("tbody tr").Count == 1, WaitTimeout);

        await component.InvokeAsync(() => searchInput.Input(""));

        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);
    }
}
