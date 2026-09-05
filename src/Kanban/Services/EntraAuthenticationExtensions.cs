using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Kanban.Services;

public static class EntraAuthenticationExtensions
{
    // Split out from Program.cs so the conditional-registration decision can be exercised in a
    // unit test (via a bare ServiceCollection + IAuthenticationSchemeProvider) without needing a
    // full app host — WebApplicationFactory's config overrides don't take effect until after
    // Program.cs's own top-level code has already made this decision.
    public static AuthenticationBuilder AddEntraIfConfigured(this AuthenticationBuilder builder, EntraSettings settings)
    {
        if (!settings.IsConfigured)
        {
            return builder;
        }

        return builder.AddOpenIdConnect(EntraSettings.SchemeName, EntraSettings.DisplayName, options =>
        {
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.Authority = $"https://login.microsoftonline.com/{settings.TenantId}/v2.0";
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.SaveTokens = true;
            options.CallbackPath = "/signin-entra";
            options.Scope.Add("email");
        });
    }
}
