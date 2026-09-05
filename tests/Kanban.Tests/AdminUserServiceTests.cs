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

    [Fact]
    public async Task GetUsersAsync_ForUserNotLockedOut_ReportsNotLockedOut()
    {
        var email = "notlockedout@example.com";
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        var users = await service.GetUsersAsync();

        var summary = Assert.Single(users, u => u.Email == email);
        Assert.False(summary.IsLockedOut);
    }

    [Fact]
    public async Task SetAdminRoleAsync_True_GrantsAdminRole()
    {
        var email = "promoteme@example.com";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        await service.SetAdminRoleAsync(userId, isAdmin: true);

        var users = await service.GetUsersAsync();
        var summary = Assert.Single(users, u => u.Email == email);
        Assert.True(summary.IsAdmin);
    }

    [Fact]
    public async Task SetAdminRoleAsync_False_RevokesAdminRole()
    {
        var email = "demoteme@example.com";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        await service.SetAdminRoleAsync(userId, isAdmin: true);
        await service.SetAdminRoleAsync(userId, isAdmin: false);

        var users = await service.GetUsersAsync();
        var summary = Assert.Single(users, u => u.Email == email);
        Assert.False(summary.IsAdmin);
    }

    [Fact]
    public async Task SetAdminRoleAsync_False_RevokesBootstrapSeededAdminRole()
    {
        var email = "bootstrapdemote@example.com";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        using (var scope = _factory.Services.CreateScope())
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminEmails:0"] = email })
                .Build();
            await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);
        }

        using var serviceScope = _factory.Services.CreateScope();
        var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
        await service.SetAdminRoleAsync(userId, isAdmin: false);

        var users = await service.GetUsersAsync();
        var summary = Assert.Single(users, u => u.Email == email);
        Assert.False(summary.IsAdmin);
    }

    [Fact]
    public async Task SetLockedOutAsync_True_ReportsLockedOutAndBlocksPasswordSignIn()
    {
        var email = "lockme@example.com";
        var password = "TestPassword123!";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, password);

        using (var serviceScope = _factory.Services.CreateScope())
        {
            var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
            await service.SetLockedOutAsync(userId, lockedOut: true);

            var users = await service.GetUsersAsync();
            var summary = Assert.Single(users, u => u.Email == email);
            Assert.True(summary.IsLockedOut);
        }

        var result = await CheckPasswordSignInAsync(userId, password);
        Assert.True(result.IsLockedOut);
    }

    [Fact]
    public async Task SetLockedOutAsync_False_LiftsLockoutAndRestoresSignIn()
    {
        var email = "unlockme@example.com";
        var password = "TestPassword123!";
        var userId = await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, password);

        using (var serviceScope = _factory.Services.CreateScope())
        {
            var service = serviceScope.ServiceProvider.GetRequiredService<IAdminUserService>();
            await service.SetLockedOutAsync(userId, lockedOut: true);
            await service.SetLockedOutAsync(userId, lockedOut: false);

            var users = await service.GetUsersAsync();
            var summary = Assert.Single(users, u => u.Email == email);
            Assert.False(summary.IsLockedOut);
        }

        var result = await CheckPasswordSignInAsync(userId, password);
        Assert.True(result.Succeeded);
    }

    private async Task<SignInResult> CheckPasswordSignInAsync(string userId, string password)
    {
        using var signInScope = _factory.Services.CreateScope();
        var signInManager = signInScope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await signInScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId);
        return await signInManager.CheckPasswordSignInAsync(user!, password, lockoutOnFailure: false);
    }
}
