using Microsoft.EntityFrameworkCore;
using Kanban.Data;
using Testcontainers.PostgreSql;

namespace Kanban.Tests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();
    private ApplicationDbContext? _dbContext;

    public ApplicationDbContext DbContext
    {
        get => _dbContext ?? throw new InvalidOperationException("Database not initialized");
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _container.StopAsync();
    }
}
