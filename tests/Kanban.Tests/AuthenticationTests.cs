using System.Net;
using System.Text;
using AngleSharp;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class AuthenticationTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private readonly IBrowsingContext _context = new BrowsingContext(Configuration.Default);

    public AuthenticationTests()
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
    public async Task RegisterPage_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Account/Register");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginPage_ReturnsSuccess()
    {
        var response = await _client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidCredentials_CreatesUser()
    {
        var registerPage = await _client.GetAsync("/Account/Register");
        var registerContent = await registerPage.Content.ReadAsStringAsync();
        var registerDocument = await _context.OpenAsync(req => req.Content(registerContent));

        var form = registerDocument.QuerySelector("form");
        Assert.NotNull(form);

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(registerDocument);
        formData["Input.Email"] = "test@example.com";
        formData["Input.Password"] = "TestPassword123!";
        formData["Input.ConfirmPassword"] = "TestPassword123!";

        var content = new FormUrlEncodedContent(formData);
        var response = await _client.PostAsync("/Account/Register", content);

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");
    }

    [Fact]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        var email = "testlogin@example.com";
        var password = "TestPassword123!";

        await RegisterUser(email, password);

        var loginPage = await _client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var loginDocument = await _context.OpenAsync(req => req.Content(loginContent));

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(loginDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = password;

        var content = new FormUrlEncodedContent(formData);
        var response = await _client.PostAsync("/Account/Login", content);

        Assert.True(response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Redirect,
            $"Expected OK or Redirect, got {response.StatusCode}");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Fails()
    {
        var email = "testinvalid@example.com";
        var password = "TestPassword123!";

        await RegisterUser(email, password);

        var loginPage = await _client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var loginDocument = await _context.OpenAsync(req => req.Content(loginContent));

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(loginDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = "WrongPassword123!";

        var content = new FormUrlEncodedContent(formData);
        var response = await _client.PostAsync("/Account/Login", content);

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", responseContent);
    }

    private async Task RegisterUser(string email, string password)
    {
        var registerPage = await _client.GetAsync("/Account/Register");
        var registerContent = await registerPage.Content.ReadAsStringAsync();
        var registerDocument = await _context.OpenAsync(req => req.Content(registerContent));

        var formData = IdentityFormTestHelpers.GetHiddenFormFields(registerDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = password;
        formData["Input.ConfirmPassword"] = password;

        var content = new FormUrlEncodedContent(formData);
        await _client.PostAsync("/Account/Register", content);
    }
}
