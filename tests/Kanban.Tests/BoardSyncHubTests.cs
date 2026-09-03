using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Kanban.Data;
using Kanban.Data.Entities;
using Kanban.Tests.Fixtures;

namespace Kanban.Tests;

public class BoardSyncHubTests : IAsyncLifetime
{
    private readonly KanbanWebApplicationFactory _factory;
    private string _userId = null!;

    public BoardSyncHubTests()
    {
        _factory = new KanbanWebApplicationFactory();
    }

    public async Task InitializeAsync()
    {
        _userId = Guid.NewGuid().ToString();
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(new ApplicationUser { Id = _userId, UserName = $"user_{_userId}", Email = $"user_{_userId}@example.com" });
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Connect_WithValidToken_Succeeds()
    {
        await using var connection = BuildConnection(CreateToken(_userId));

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
        await connection.StopAsync();
    }

    [Fact]
    public async Task Connect_WithoutToken_Fails()
    {
        await using var connection = BuildConnection(token: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    private HubConnection BuildConnection(string? token)
    {
        return new HubConnectionBuilder()
            .WithUrl($"{_factory.Server.BaseAddress}sync", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                // TestServer doesn't support real WebSocket upgrades; force LongPolling so the
                // in-memory handler can service the transport like it does plain HTTP requests.
                options.Transports = HttpTransportType.LongPolling;
                if (token != null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
    }

    private string CreateToken(string userId)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
        var key = new UTF8Encoding().GetBytes("test-secret-key-long-enough-for-hs256");
        var creds = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
