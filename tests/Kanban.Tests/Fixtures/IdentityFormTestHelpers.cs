using AngleSharp;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;

namespace Kanban.Tests.Fixtures;

internal static class IdentityFormTestHelpers
{
    public static async Task RegisterUserAsync(HttpClient client, string email, string password)
    {
        var context = new BrowsingContext(Configuration.Default);
        var registerPage = await client.GetAsync("/Account/Register");
        var registerContent = await registerPage.Content.ReadAsStringAsync();
        var registerDocument = await context.OpenAsync(req => req.Content(registerContent));

        var formData = GetHiddenFormFields(registerDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = password;
        formData["Input.ConfirmPassword"] = password;

        await client.PostAsync("/Account/Register", new FormUrlEncodedContent(formData));
    }

    public static async Task ConfirmEmailAsync(IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            throw new InvalidOperationException($"No user found for '{email}'.");
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await userManager.ConfirmEmailAsync(user, token);
    }

    public static async Task LoginUserAsync(HttpClient client, string email, string password)
    {
        var context = new BrowsingContext(Configuration.Default);
        var loginPage = await client.GetAsync("/Account/Login");
        var loginContent = await loginPage.Content.ReadAsStringAsync();
        var loginDocument = await context.OpenAsync(req => req.Content(loginContent));

        var formData = GetHiddenFormFields(loginDocument);
        formData["Input.Email"] = email;
        formData["Input.Password"] = password;

        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(formData));
    }

    public static async Task<string> RegisterConfirmAndLoginAsync(HttpClient client, IServiceProvider services, string email, string password)
    {
        await RegisterUserAsync(client, email, password);
        await ConfirmEmailAsync(services, email);
        await LoginUserAsync(client, email, password);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            throw new InvalidOperationException($"No user found for '{email}'.");
        }

        return user.Id;
    }

    public static Dictionary<string, string> GetHiddenFormFields(IDocument document)
    {
        var fields = new Dictionary<string, string>();
        foreach (var input in document.QuerySelectorAll("input[type='hidden']"))
        {
            var name = input.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = input.GetAttribute("value") ?? string.Empty;
        }

        return fields;
    }

    public static Dictionary<string, string> GetHiddenFormFields(IElement form)
    {
        var fields = new Dictionary<string, string>();
        foreach (var input in form.QuerySelectorAll("input[type='hidden']"))
        {
            var name = input.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
                continue;

            fields[name] = input.GetAttribute("value") ?? string.Empty;
        }

        return fields;
    }
}
