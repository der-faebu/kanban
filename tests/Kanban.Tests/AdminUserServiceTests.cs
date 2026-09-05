using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class AdminUserServiceTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AdminUserServiceTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetUsersAsync_ForPasswordUser_ReportsPasswordOnly()
    {
        var email = "plainuser@example.com";
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.True(summary.HasPassword);
        Assert.False(summary.HasPasskey);
        Assert.False(summary.HasEntraLogin);
        Assert.False(summary.IsAdmin);
    }

    [Fact]
    public async Task GetUsersAsync_ForAdminUser_ReportsIsAdmin()
    {
        var email = "adminlisted@example.com";
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminEmails:0"] = email })
                .Build();
            await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);
        }

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.True(summary.IsAdmin);
    }

    [Fact]
    public async Task GetUsersAsync_ForUserWithPasskey_ReportsHasPasskey()
    {
        var email = "passkeylisted@example.com";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            var passkeyInfo = new UserPasskeyInfo(
                credentialId: Guid.NewGuid().ToByteArray(),
                publicKey: "test-public-key"u8.ToArray(),
                createdAt: DateTimeOffset.UtcNow,
                signCount: 0,
                transports: [],
                isUserVerified: true,
                isBackupEligible: false,
                isBackedUp: false,
                attestationObject: "test-attestation"u8.ToArray(),
                clientDataJson: "test-client-data"u8.ToArray())
            {
                Name = "Test device",
            };
            await userManager.AddOrUpdatePasskeyAsync(user!, passkeyInfo);
        }

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.True(summary.HasPasskey);
    }

    [Fact]
    public async Task GetUsersAsync_ForUserWithEntraLogin_ReportsHasEntraLogin()
    {
        var email = "entralisted@example.com";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, email)], "TestExternal"));
            var info = new ExternalLoginInfo(principal, EntraSettings.SchemeName, "entra-key-1", EntraSettings.DisplayName);
            await userManager.AddLoginAsync(user!, info);
        }

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.True(summary.HasEntraLogin);
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsCreatedAtCloseToRegistrationTime()
    {
        var email = "createdat@example.com";
        var before = DateTime.UtcNow.AddSeconds(-5);
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");
        var after = DateTime.UtcNow.AddSeconds(5);

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.InRange(summary.CreatedAt, before, after);
    }
}
