using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
        builder.ConfigureServices(services =>
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            if (!_containerStarted)
            {
                _container.StartAsync().GetAwaiter().GetResult();
                _containerStarted = true;
            }

            var connectionString = _container.GetConnectionString();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));
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
