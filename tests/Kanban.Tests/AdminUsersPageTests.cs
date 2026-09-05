using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class AdminUsersPageTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public AdminUsersPageTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task AdminUsersPage_AsAnonymous_IsRejected()
    {
        var response = await _client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AdminUsersPage_AsNonAdminUser_IsRejected()
    {
        var email = "nonadminvisitor@example.com";
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        var response = await _client.GetAsync("/admin/users");

        // Authenticated-but-forbidden goes through Identity's cookie-auth Forbid path
        // (AccessDeniedPath), distinct from the anonymous case's Challenge/Login redirect above.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task AdminUsersPage_AsAdmin_ListsUsers()
    {
        var email = "adminvisitor@example.com";
        await RegisterAndPromoteToAdminAsync(email);

        var response = await _client.GetAsync("/admin/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains(email, content);
        Assert.Contains("Password", content);
    }

    [Fact]
    public async Task NavMenu_ForNonAdminUser_HasNoLinkToAdminUsersPage()
    {
        var email = "noadminlink@example.com";
        await IdentityFormTestHelpers.RegisterConfirmAndLoginAsync(_client, _factory.Services, email, "TestPassword123!");

        var response = await _client.GetAsync("/boards");
        var content = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("admin/users", content);
    }

    [Fact]
    public async Task NavMenu_ForAdminUser_HasLinkToAdminUsersPage()
    {
        var email = "hasadminlink@example.com";
        await RegisterAndPromoteToAdminAsync(email);

        var response = await _client.GetAsync("/boards");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("admin/users", content);
    }

    private async Task RegisterAndPromoteToAdminAsync(string email)
    {
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");
        await IdentityFormTestHelpers.ConfirmEmailAsync(_factory.Services, email);

        using (var scope = _factory.Services.CreateScope())
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AdminEmails:0"] = email })
                .Build();
            await AdminRoleSeeder.SeedAsync(scope.ServiceProvider, config);
        }

        await IdentityFormTestHelpers.LoginUserAsync(_client, email, "TestPassword123!");
    }
}
