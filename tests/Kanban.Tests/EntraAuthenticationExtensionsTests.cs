using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Services;

namespace Kanban.Tests;

public class EntraAuthenticationExtensionsTests
{
    [Fact]
    public async Task AddEntraIfConfigured_WithCompleteSettings_RegistersMicrosoftScheme()
    {
        var settings = new EntraSettings("tenant-id", "client-id", "client-secret");

        var provider = BuildProviderWithEntra(settings);
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        var scheme = await schemeProvider.GetSchemeAsync(EntraSettings.SchemeName);

        Assert.NotNull(scheme);
        Assert.Equal(EntraSettings.DisplayName, scheme!.DisplayName);
    }

    [Fact]
    public async Task AddEntraIfConfigured_WithIncompleteSettings_RegistersNoScheme()
    {
        var settings = new EntraSettings("tenant-id", null, null);

        var provider = BuildProviderWithEntra(settings);
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        var scheme = await schemeProvider.GetSchemeAsync(EntraSettings.SchemeName);

        Assert.Null(scheme);
    }

    private static ServiceProvider BuildProviderWithEntra(EntraSettings settings)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddEntraIfConfigured(settings);
        return services.BuildServiceProvider();
    }
}
