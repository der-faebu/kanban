using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MudBlazor;
using MudBlazor.Services;
using Kanban.Components.Board;
using Kanban.Data;
using Kanban.Services;

namespace Kanban.Tests.Fixtures;

/// <summary>
/// bUnit harness for rendering <see cref="CardDetail"/> against real service implementations,
/// resolved from <see cref="KanbanWebApplicationFactory"/>'s DI container (the same
/// Testcontainers-backed Postgres instance the HTTP API integration tests use) instead of a
/// mocking library, matching this repo's existing no-mocking test convention.
/// </summary>
public class CardDetailTestContext : BunitContext, IAsyncLifetime
{
    public KanbanWebApplicationFactory Factory { get; } = new();
    public string UserId { get; } = Guid.NewGuid().ToString();

    private IServiceScope? _scope;

    async Task IAsyncLifetime.InitializeAsync()
    {
        // Accessing Server forces WebApplicationFactory to build its host (and start the
        // Testcontainers Postgres container) before we pull services out of it.
        _ = Factory.Server;
        _scope = Factory.Services.CreateScope();

        Services.AddMudServices();
        // MudBlazor components call into JS (popover positioning, key interceptors) that bUnit
        // can't service; Loose mode returns default values instead of throwing so those calls
        // don't fail the render.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthenticationStateProvider(UserId));
        RegisterScopedService<ICardService>();
        RegisterScopedService<ILabelService>();
        RegisterScopedService<IProjectService>();
        RegisterScopedService<IBoardService>();
        RegisterScopedService<ICommentService>();
        RegisterScopedService<ITimeTrackingService>();
        RegisterScopedService<IChecklistService>();
        RegisterScopedService<IAttachmentService>();
        RegisterScopedService<IActivityLogService>();
        RegisterScopedService<IOptions<AttachmentsOptions>>();

        var dbContext = DbContext;
        dbContext.Users.Add(new ApplicationUser { Id = UserId, UserName = $"user_{UserId}", Email = $"user_{UserId}@example.com" });
        await dbContext.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        _scope?.Dispose();
        await Factory.DisposeAsync();
        // Async, not the sync Dispose(): MudBlazor's PopoverService only implements
        // IAsyncDisposable, and bUnit's synchronous Dispose() throws trying to dispose it.
        await base.DisposeAsync();
    }

    /// <summary>Same DbContext instance for the lifetime of the test's scope, for seeding and assertions.</summary>
    public ApplicationDbContext DbContext => _scope!.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    /// <summary>Resolves TService from the test's factory scope and registers that same instance into bUnit's container.</summary>
    private void RegisterScopedService<TService>() where TService : class =>
        Services.AddSingleton(_scope!.ServiceProvider.GetRequiredService<TService>());

    /// <summary>
    /// Renders CardDetail the same way production does: mount a MudDialogProvider (plus a
    /// MudPopoverProvider, since MudBlazor 7+ portals all popover content -- including MudMenu's
    /// dropdown lists -- into that provider rather than rendering it inline), then open the dialog
    /// through IDialogService.ShowAsync (see BoardView.razor's OpenCardDialog and MainLayout.razor's
    /// provider setup), rather than rendering CardDetail directly. CardDetail's own &lt;MudDialog&gt;
    /// only renders its content when MudBlazor's *internal* dialog-instance cascading value is
    /// present (MudDialog.IsInline = IsNested || DialogInstance is null) -- that type isn't public,
    /// so it can't be faked from outside MudBlazor's assembly. Going through the real
    /// DialogService/MudDialogProvider gets CardDetail the genuine cascading values it needs,
    /// without reaching into MudBlazor internals.
    /// </summary>
    public async Task<IRenderedComponent<CardDetail>> RenderCardDetailAsync(int boardId, int cardId, string boardName)
    {
        PopoverProvider = Render<MudPopoverProvider>();
        var provider = Render<MudDialogProvider>();
        var dialogService = Services.GetRequiredService<IDialogService>();

        var parameters = new DialogParameters<CardDetail>
        {
            { x => x.BoardId, boardId },
            { x => x.CardId, cardId },
            { x => x.BoardName, boardName },
        };
        await dialogService.ShowAsync<CardDetail>(string.Empty, parameters);

        // Generous timeout: this suite runs dozens of Testcontainers-backed test classes in
        // parallel, and bUnit's default 1s WaitForState timeout is too tight under that load.
        provider.WaitForState(() => provider.FindComponents<CardDetail>().Count > 0, TimeSpan.FromSeconds(30));
        return provider.FindComponent<CardDetail>();
    }

    /// <summary>
    /// MudPopoverProvider portals all MudMenu dropdown content (and other popovers) into its own
    /// separate render tree rather than nesting it under the activating component, so tests that
    /// open a MudMenu must search here -- not on the CardDetail component returned by
    /// RenderCardDetailAsync -- to find the popover's content. Set once RenderCardDetailAsync runs.
    /// </summary>
    public IRenderedComponent<MudPopoverProvider> PopoverProvider { get; private set; } = null!;
}
