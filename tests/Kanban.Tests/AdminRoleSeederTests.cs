using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class AdminRoleSeederTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AdminRoleSeederTests()
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
    public async Task SeedAsync_WithMatchingEmail_GrantsAdminRole()
    {
        var email = "admin1@example.com";
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");
        var config = ConfigWithAdminEmails(email);

        using var scope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.True(await roleManager.RoleExistsAsync(AdminRoleSeeder.AdminRoleName));
        Assert.True(await userManager.IsInRoleAsync(user!, AdminRoleSeeder.AdminRoleName));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateOrError()
    {
        var email = "admin2@example.com";
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");
        var config = ConfigWithAdminEmails(email);

        using var scope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var roles = await userManager.GetRolesAsync(user!);

        Assert.Single(roles, AdminRoleSeeder.AdminRoleName);
    }

    [Fact]
    public async Task SeedAsync_WithEmailMatchingNoUser_SkipsSilently()
    {
        var config = ConfigWithAdminEmails("nobody-yet@example.com");

        using var scope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("nobody-yet@example.com");
        Assert.Null(user);
    }

    [Fact]
    public async Task SeedAsync_AfterAccountIsCreatedLater_PicksUpOnNextRun()
    {
        var email = "admin3@example.com";
        var config = ConfigWithAdminEmails(email);

        using (var firstRunScope = _factory.Services.CreateScope())
        {
            await AdminRoleSeeder.SeedAsync(firstRunScope.ServiceProvider, config);
        }

        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");

        using var secondRunScope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(secondRunScope.ServiceProvider, config);

        var userManager = secondRunScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.True(await userManager.IsInRoleAsync(user!, AdminRoleSeeder.AdminRoleName));
    }

    [Fact]
    public async Task SignedInAdmin_PrincipalCarriesAdminRoleClaim()
    {
        var email = "admin5@example.com";
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");
        var config = ConfigWithAdminEmails(email);

        using var scope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        var principal = await signInManager.CreateUserPrincipalAsync(user!);

        Assert.Contains(principal.Claims, c => c.Type == ClaimTypes.Role && c.Value == AdminRoleSeeder.AdminRoleName);
    }

    [Fact]
    public async Task AdminPolicy_RequiresAdminRole()
    {
        var email = "admin4@example.com";
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");
        var config = ConfigWithAdminEmails(email);

        using var scope = _factory.Services.CreateScope();
        await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);

        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var adminIdentity = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, AdminRoleSeeder.AdminRoleName)], "TestAuth"));
        var nonAdminIdentity = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "someone")], "TestAuth"));

        var adminResult = await authorizationService.AuthorizeAsync(adminIdentity, AdminRoleSeeder.AdminPolicyName);
        var nonAdminResult = await authorizationService.AuthorizeAsync(nonAdminIdentity, AdminRoleSeeder.AdminPolicyName);

        Assert.True(adminResult.Succeeded);
        Assert.False(nonAdminResult.Succeeded);
    }

    private static Microsoft.Extensions.Configuration.IConfiguration ConfigWithAdminEmails(params string[] emails)
    {
        var data = new Dictionary<string, string?>();
        for (var i = 0; i < emails.Length; i++)
        {
            data[$"AdminEmails:{i}"] = emails[i];
        }
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }
}
