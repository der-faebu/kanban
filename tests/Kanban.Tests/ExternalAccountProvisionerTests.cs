using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Kanban.Services;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class ExternalAccountProvisionerTests : IAsyncLifetime
{
    private const string LoginProvider = "Entra";
    private const string DisplayName = "Microsoft";

    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;

    public ExternalAccountProvisionerTests()
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
    public async Task SignInOrProvisionAsync_WithNoMatchingUserAndEmailClaim_CreatesConfirmedLinkedAccountAndSignsIn()
    {
        var email = "newviaentra@example.com";
        var info = ExternalLoginInfoFor(email, providerKey: "entra-sub-1");

        using var scope = _factory.Services.CreateScope();

        var result = await InvokeProvisionerAsync(scope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.SignedIn, result.Outcome);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);

        var logins = await userManager.GetLoginsAsync(user);
        Assert.Contains(logins, l => l.LoginProvider == LoginProvider && l.ProviderKey == "entra-sub-1");
    }

    [Fact]
    public async Task SignInOrProvisionAsync_WithExistingAccountMatchingEmail_LinksInsteadOfDuplicating()
    {
        var email = "existinguser@example.com";
        await IdentityFormTestHelpers.RegisterUserAsync(_client, email, "TestPassword123!");

        using (var confirmScope = _factory.Services.CreateScope())
        {
            var confirmUserManager = confirmScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var registeredUser = await confirmUserManager.FindByEmailAsync(email);
            Assert.NotNull(registeredUser);
            var token = await confirmUserManager.GenerateEmailConfirmationTokenAsync(registeredUser!);
            await confirmUserManager.ConfirmEmailAsync(registeredUser!, token);
        }

        var info = ExternalLoginInfoFor(email, providerKey: "entra-sub-2");

        using var scope = _factory.Services.CreateScope();
        var result = await InvokeProvisionerAsync(scope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.SignedIn, result.Outcome);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usersWithEmail = await userManager.Users
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .ToListAsync();
        Assert.Single(usersWithEmail);

        var user = usersWithEmail[0];
        var logins = await userManager.GetLoginsAsync(user);
        Assert.Contains(logins, l => l.LoginProvider == LoginProvider && l.ProviderKey == "entra-sub-2");
    }

    [Fact]
    public async Task SignInOrProvisionAsync_CalledTwiceForSameLogin_SignsInWithoutDuplicatingLogins()
    {
        var email = "repeatlogin@example.com";
        var info = ExternalLoginInfoFor(email, providerKey: "entra-sub-3");

        using var scope = _factory.Services.CreateScope();

        var first = await InvokeProvisionerAsync(scope.ServiceProvider, info);
        var second = await InvokeProvisionerAsync(scope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.SignedIn, first.Outcome);
        Assert.Equal(ExternalSignInOutcome.SignedIn, second.Outcome);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var logins = await userManager.GetLoginsAsync(user!);
        Assert.Single(logins);
    }

    [Fact]
    public async Task SignInOrProvisionAsync_WithNoEmailClaim_NeedsManualRegistration()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "entra-sub-4")], "TestExternal"));
        var info = new ExternalLoginInfo(principal, LoginProvider, "entra-sub-4", DisplayName);

        using var scope = _factory.Services.CreateScope();

        var result = await InvokeProvisionerAsync(scope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.NeedsManualRegistration, result.Outcome);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var users = await userManager.Users.ToListAsync();
        Assert.DoesNotContain(users, u => u.Id == "entra-sub-4");
    }

    [Fact]
    public async Task SignInOrProvisionAsync_WithLockedOutExistingLink_ReturnsLockedOut()
    {
        var email = "lockedout@example.com";
        var info = ExternalLoginInfoFor(email, providerKey: "entra-sub-5");

        using var firstScope = _factory.Services.CreateScope();
        var firstResult = await InvokeProvisionerAsync(firstScope.ServiceProvider, info);
        Assert.Equal(ExternalSignInOutcome.SignedIn, firstResult.Outcome);

        var userManager = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        await userManager.SetLockoutEnabledAsync(user!, true);
        await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1));

        using var secondScope = _factory.Services.CreateScope();
        var secondResult = await InvokeProvisionerAsync(secondScope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.LockedOut, secondResult.Outcome);
    }

    [Fact]
    public async Task SignInOrProvisionAsync_WithTwoFactorEnabledOnExistingLink_ReturnsRequiresTwoFactor()
    {
        var email = "twofactor@example.com";
        var info = ExternalLoginInfoFor(email, providerKey: "entra-sub-6");

        using var firstScope = _factory.Services.CreateScope();
        var firstResult = await InvokeProvisionerAsync(firstScope.ServiceProvider, info);
        Assert.Equal(ExternalSignInOutcome.SignedIn, firstResult.Outcome);

        var userManager = firstScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        await userManager.SetTwoFactorEnabledAsync(user!, true);

        using var secondScope = _factory.Services.CreateScope();
        var secondResult = await InvokeProvisionerAsync(secondScope.ServiceProvider, info);

        Assert.Equal(ExternalSignInOutcome.RequiresTwoFactor, secondResult.Outcome);
    }

    // ExternalLoginSignInAsync's success path signs in via the real SignInManager, which needs
    // a live HttpContext to write the auth cookie into. Outside an actual HTTP request (as here,
    // calling the provisioner directly) there isn't one, so stage a throwaway one on the
    // AsyncLocal-backed IHttpContextAccessor for the duration of the call — this doesn't leak
    // across tests since AsyncLocal only flows within this call's own async chain.
    private static async Task<ExternalSignInResult> InvokeProvisionerAsync(IServiceProvider services, ExternalLoginInfo info)
    {
        var httpContextAccessor = services.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = services };
        try
        {
            var provisioner = services.GetRequiredService<IExternalAccountProvisioner>();
            return await provisioner.SignInOrProvisionAsync(info);
        }
        finally
        {
            httpContextAccessor.HttpContext = null;
        }
    }

    private static ExternalLoginInfo ExternalLoginInfoFor(string email, string providerKey)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, providerKey),
                new Claim(ClaimTypes.Email, email),
            ], "TestExternal"));
        return new ExternalLoginInfo(principal, LoginProvider, providerKey, DisplayName);
    }
}
