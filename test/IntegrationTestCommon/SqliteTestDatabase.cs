using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.IntegrationTestCommon;

public class SqliteTestDatabase : ITestDatabase
{
    private SqliteConnection SqliteConnection { get; set; }

    public SqliteTestDatabase()
    {
        SqliteConnection = new SqliteConnection("DataSource=:memory:");
        SqliteConnection.Open();
    }

    public void AddDatabase(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(s => new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(SqliteConnection)
            .UseApplicationServiceProvider(s)
            .Options);
    }

    public void Migrate(IServiceCollection serviceCollection)
    {
        var serviceProvider = serviceCollection.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<DatabaseContext>();

        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.Dispose();
    }
}
