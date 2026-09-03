using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Kanban.Tests.Fixtures;

public class FakeAuthenticationStateProvider(string userId) : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "Test");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
    }
}
