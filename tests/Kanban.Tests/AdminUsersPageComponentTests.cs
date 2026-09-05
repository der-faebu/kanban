using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Kanban.Components.Pages.Admin;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

// bUnit coverage for what the HTTP-level AdminUsersPageTests can't prove: typing into the search
// box re-filters the rendered rows (MudTable's Filter runs client-side, so a server-rendered HTML
// snapshot only ever shows the unfiltered initial list), and the promote/lockout toggle buttons
// call through to the service and immediately re-render the row's updated status.
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

    [Fact]
    public async Task PromoteButton_GrantsAdminRoleAndFlipsToDemote()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "admin-toggle");

        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Demote"), WaitTimeout);
        Assert.Contains("Yes", FindRow(component, "alice@example.com").TextContent);
        Assert.DoesNotContain("Demote", FindRow(component, "bob@example.com").TextContent);
    }

    [Fact]
    public async Task DemoteButton_RevokesAdminRoleAndFlipsToPromote()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "admin-toggle");
        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Demote"), WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "admin-toggle");

        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Promote"), WaitTimeout);
        Assert.DoesNotContain("Yes", FindRow(component, "alice@example.com").TextContent);
    }

    [Fact]
    public async Task LockButton_LocksUserOutAndFlipsToUnlock()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "lockout-toggle");

        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Unlock"), WaitTimeout);
        Assert.Contains("Locked out", FindRow(component, "alice@example.com").TextContent);
        Assert.DoesNotContain("Locked out", FindRow(component, "bob@example.com").TextContent);
    }

    [Fact]
    public async Task UnlockButton_LiftsLockoutAndFlipsToLock()
    {
        var component = Render<Users>();
        component.WaitForState(() => component.FindAll("tbody tr").Count == 2, WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "lockout-toggle");
        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Unlock"), WaitTimeout);

        await ClickRowButtonAsync(component, "alice@example.com", "lockout-toggle");

        component.WaitForState(() => FindRow(component, "alice@example.com").TextContent.Contains("Lock") && !FindRow(component, "alice@example.com").TextContent.Contains("Unlock"), WaitTimeout);
        Assert.Contains("Active", FindRow(component, "alice@example.com").TextContent);
    }

    private static IElement FindRow(IRenderedComponent<Users> component, string email) =>
        component.FindAll("tbody tr").Single(row => row.TextContent.Contains(email));

    private static async Task ClickRowButtonAsync(IRenderedComponent<Users> component, string email, string buttonClass) =>
        await component.InvokeAsync(() => FindRow(component, email).QuerySelector($"button.{buttonClass}")!.Click());
}
