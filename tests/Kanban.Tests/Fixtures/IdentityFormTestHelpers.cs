using AngleSharp;
using AngleSharp.Dom;

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
