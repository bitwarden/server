using System.Reflection;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;
using EfUser = Bit.Infrastructure.EntityFramework.Models.User;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

/// <summary>
/// Keeps <see cref="UserBulkRow"/> in step with the EF <c>User</c> model. A column the entity maps but the
/// projection lacks is silently left at its default by the unprotected insert; no behavioural test can see that.
/// The opposite mismatch fails the insert itself, which <see cref="BulkCommitterTests"/> covers.
/// </summary>
public sealed class UserBulkRowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly DatabaseContext _db;

    public UserBulkRowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<DatabaseContext>(opts =>
        {
            opts.UseSqlite(_connection);

            // The model's converters close over this provider's IDataProtector; keep it out of EF's shared cache.
            opts.ReplaceService<IModelCacheKeyFactory, NonCachingModelCacheKeyFactory>();
        });

        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<DatabaseContext>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void UserBulkRow_HasEveryColumnTheEfUserEntityMaps()
    {
        Assert.Empty(EfUserColumns().Except(UserBulkRowColumns()));
    }

    private IEnumerable<string> EfUserColumns() =>
        _db.Model.FindEntityType(typeof(EfUser))!.GetProperties().Select(p => p.Name);

    private static IEnumerable<string> UserBulkRowColumns() =>
        typeof(UserBulkRow).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);

    private sealed class NonCachingModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => new object();
    }
}
