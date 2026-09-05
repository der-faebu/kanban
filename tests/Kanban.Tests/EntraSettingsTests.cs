using Microsoft.Extensions.Configuration;
using Kanban.Services;

namespace Kanban.Tests;

public class EntraSettingsTests
{
    [Fact]
    public void FromConfiguration_WithAllThreeValues_IsConfigured()
    {
        var config = BuildConfig(tenantId: "tenant", clientId: "client", clientSecret: "secret");

        var settings = EntraSettings.FromConfiguration(config);

        Assert.True(settings.IsConfigured);
    }

    [Theory]
    [InlineData(null, "client", "secret")]
    [InlineData("tenant", null, "secret")]
    [InlineData("tenant", "client", null)]
    [InlineData("", "client", "secret")]
    [InlineData("   ", "client", "secret")]
    public void FromConfiguration_WithAnyValueMissing_IsNotConfigured(string? tenantId, string? clientId, string? clientSecret)
    {
        var config = BuildConfig(tenantId, clientId, clientSecret);

        var settings = EntraSettings.FromConfiguration(config);

        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void FromConfiguration_WithNoEntraSection_IsNotConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var settings = EntraSettings.FromConfiguration(config);

        Assert.False(settings.IsConfigured);
    }

    private static IConfiguration BuildConfig(string? tenantId, string? clientId, string? clientSecret)
    {
        var data = new Dictionary<string, string?>();
        if (tenantId is not null) data["Entra:TenantId"] = tenantId;
        if (clientId is not null) data["Entra:ClientId"] = clientId;
        if (clientSecret is not null) data["Entra:ClientSecret"] = clientSecret;
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }
}
