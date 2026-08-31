using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kanban.Data;
using Testcontainers.PostgreSql;

namespace Kanban.Tests.Fixtures;

public class KanbanWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();
    private bool _containerStarted = false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "test",
                ["Jwt:Audience"] = "test",
                ["Jwt:Key"] = "test-secret-key-long-enough-for-hs256",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs registers ApplicationDbContext via AddDbContextFactory (singleton
            // factory) plus a scoped wrapper around it. Swapping in AddDbContext here (which
            // registers scoped DbContextOptions) leaves the app's singleton factory pointing at
            // options of the wrong lifetime, so remove all three descriptors and re-register the
            // same factory-based shape Program.cs uses, just pointed at the test container.
            foreach (var serviceType in new[]
                     {
                         typeof(DbContextOptions<ApplicationDbContext>),
                         typeof(IDbContextFactory<ApplicationDbContext>),
                         typeof(ApplicationDbContext)
                     })
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
            }

            if (!_containerStarted)
            {
                _container.StartAsync().GetAwaiter().GetResult();
                _containerStarted = true;
            }

            var connectionString = _container.GetConnectionString();
            services.AddDbContextFactory<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
            services.AddScoped<ApplicationDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        if (_containerStarted)
        {
            await _container.StopAsync();
        }
        await base.DisposeAsync();
    }
}
