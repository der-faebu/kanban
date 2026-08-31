using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kanban.Endpoints;

public static class EndpointAuthExtensions
{
    public static RouteGroupBuilder RequireJwtAuthorization(this RouteGroupBuilder group)
    {
        return group
            .RequireAuthorization(policy =>
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                      .RequireAuthenticatedUser())
            .DisableAntiforgery();
    }
}
