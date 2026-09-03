using System.Net;
using System.Text;
using AngleSharp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class PasskeyTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly IBrowsingContext _context = new BrowsingContext(Configuration.Default);

    public PasskeyTests()
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
    public async Task PasskeysPage_AsAnonymous_RedirectsToLogin()
    {
        var response = await _client.GetAsync("/Account/Manage/Passkeys");

        Assert.True(response.RequestMessage!.RequestUri!.AbsolutePath.Contains("Account/Login", StringComparison.OrdinalIgnoreCase),
            $"Expected a redirect to the login page, ended up at {response.RequestMessage!.RequestUri}");
    }

    [Fact]
    public async Task PasskeysPage_WithNoPasskeys_ShowsEmptyState()
    {
        var email = "nopasskeys@example.com";
        await RegisterAndLoginAsync(email, "TestPassword123!");

        var response = await _client.GetAsync("/Account/Manage/Passkeys");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You don't have any passkeys yet.", content);
    }

    [Fact]
    public async Task PasskeysPage_WithSeededPasskey_ListsItsName()
    {
        var email = "haspasskey@example.com";
        var userId = await RegisterAndLoginAsync(email, "TestPassword123!");

        await SeedPasskeyAsync(userId, "My laptop");

        var response = await _client.GetAsync("/Account/Manage/Passkeys");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("My laptop", content);
    }

    [Fact]
    public async Task PasskeyCreationOptions_AsAuthenticatedUser_ReturnsPublicKeyOptions()
    {
        var email = "creationoptions@example.com";
        await RegisterAndLoginAsync(email, "TestPassword123!");

        var passkeysPage = await _client.GetAsync("/Account/Manage/Passkeys");
        var pageContent = await passkeysPage.Content.ReadAsStringAsync();
        var document = await _context.OpenAsync(req => req.Content(pageContent));

        var submitElement = document.QuerySelector("passkey-submit");
        Assert.NotNull(submitElement);
        var tokenName = submitElement!.GetAttribute("request-token-name");
        var tokenValue = submitElement.GetAttribute("request-token-value");
        Assert.False(string.IsNullOrEmpty(tokenName));
        Assert.False(string.IsNullOrEmpty(tokenValue));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/PasskeyCreationOptions");
        request.Headers.Add(tokenName!, tokenValue);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"challenge\"", body);
    }

    [Fact]
    public async Task PasskeyRequestOptions_AsAnonymous_ReturnsPublicKeyOptions()
    {
        var response = await _client.PostAsync("/Account/PasskeyRequestOptions", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"challenge\"", body);
    }

    [Fact]
    public async Task PasskeyRequestOptions_WithUsername_ReturnsPublicKeyOptions()
    {
        await RegisterAndLoginAsync("requestoptions@example.com", "TestPassword123!");

        var response = await _client.PostAsync("/Account/PasskeyRequestOptions?username=requestoptions@example.com", content: null);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"challenge\"", body);
    }

    [Fact]
    public async Task LoginPage_RendersPasskeySignInButton()
    {
        var response = await _client.GetAsync("/Account/Login");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("Sign in with a passkey", content);
        Assert.Contains("operation=\"Request\"", content);
    }

    [Fact]
    public async Task Login_WithInvalidPasskeyCredential_ShowsInlineErrorNotAnException()
    {
        var loginPage = await _client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var loginDocument = await _context.OpenAsync(req => req.Content(loginContent));

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(loginDocument);
        formData["Input.CredentialJson"] = "{ \"not\": \"a real credential\" }";

        var response = await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));
        var responseContent = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Contains("Error", responseContent);
        }
    }

    [Fact]
    public async Task RegisterMultiplePasskeys_AreEachIndependentlyRemovable()
    {
        var email = "multiplepasskeys@example.com";
        var userId = await RegisterAndLoginAsync(email, "TestPassword123!");
        await SeedPasskeyAsync(userId, "Laptop");
        await SeedPasskeyAsync(userId, "Phone");

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            var passkeys = await userManager.GetPasskeysAsync(user!);
            Assert.Equal(2, passkeys.Count);

            var laptop = Assert.Single(passkeys, p => p.Name == "Laptop");
            var removeResult = await userManager.RemovePasskeyAsync(user!, laptop.CredentialId);
            Assert.True(removeResult.Succeeded);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            var remaining = await userManager.GetPasskeysAsync(user!);

            Assert.Single(remaining);
            Assert.Equal("Phone", remaining[0].Name);
        }
    }

    [Fact]
    public async Task RemovePasskey_DeletesItFromTheDatabase()
    {
        var email = "removepasskey@example.com";
        var userId = await RegisterAndLoginAsync(email, "TestPassword123!");
        await SeedPasskeyAsync(userId, "Old phone");

        var passkeysPage = await _client.GetAsync("/Account/Manage/Passkeys");
        var pageContent = await passkeysPage.Content.ReadAsStringAsync();
        var document = await _context.OpenAsync(req => req.Content(pageContent));

        var form = document.QuerySelectorAll("form").FirstOrDefault(f =>
            f.QuerySelector("button")?.TextContent.Contains("Remove") == true);
        Assert.NotNull(form);

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(form!);
        var action = form!.GetAttribute("action") ?? "/Account/Manage/Passkeys";

        var response = await _client.PostAsync(action, new FormUrlEncodedContent(formData));
        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        var remaining = await userManager.GetPasskeysAsync(user!);

        Assert.Empty(remaining);
    }

    private async Task SeedPasskeyAsync(string userId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId);
        Assert.NotNull(user);

        var passkeyInfo = new UserPasskeyInfo(
            credentialId: Guid.NewGuid().ToByteArray(),
            publicKey: Encoding.UTF8.GetBytes("test-public-key"),
            createdAt: DateTimeOffset.UtcNow,
            signCount: 0,
            transports: [],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: Encoding.UTF8.GetBytes("test-attestation"),
            clientDataJson: Encoding.UTF8.GetBytes("test-client-data"))
        {
            Name = name
        };

        var result = await userManager.AddOrUpdatePasskeyAsync(user!, passkeyInfo);
        Assert.True(result.Succeeded);
    }

    private async Task<string> RegisterAndLoginAsync(string email, string password)
    {
        var registerPage = await _client.GetAsync("/Account/Register");
        var registerContent = await registerPage.Content.ReadAsStringAsync();
        var registerDocument = await _context.OpenAsync(req => req.Content(registerContent));

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(registerDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = password;
        formData["Input.ConfirmPassword"] = password;

        await _client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));

        using (var confirmScope = _factory.Services.CreateScope())
        {
            var confirmUserManager = confirmScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var registeredUser = await confirmUserManager.FindByEmailAsync(email);
            Assert.NotNull(registeredUser);
            var confirmationToken = await confirmUserManager.GenerateEmailConfirmationTokenAsync(registeredUser!);
            var confirmResult = await confirmUserManager.ConfirmEmailAsync(registeredUser!, confirmationToken);
            Assert.True(confirmResult.Succeeded);
        }

        var loginPage = await _client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var loginDocument = await _context.OpenAsync(req => req.Content(loginContent));

        var loginFormData = IdentityFormTestHelpers.GetHiddenFormFields(loginDocument);
        loginFormData["Input.Email"] = email;
        loginFormData["Input.Password"] = password;

        await _client.PostAsync("/Account/Login", new FormUrlEncodedContent(loginFormData));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!.Id;
    }
}
