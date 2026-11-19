using AutoMapper;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Pipeline;

/// <summary>
/// Verifies what <see cref="BulkCommitter"/> writes to the raw <c>User.Key</c> and <c>User.MasterPassword</c>
/// columns with data protection enabled and disabled, against an in-memory SQLite database.
/// </summary>
public sealed class BulkCommitterTests : IDisposable
{
    private const string _key = "2.known-key";
    private const string _masterPassword = "known-hash";
    private const string _dataProtectionPrefix = "P|";

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly DatabaseContext _db;

    public BulkCommitterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<DatabaseContext>(opts =>
        {
            opts.UseSqlite(_connection);

            // EF caches the model per context type, so a cached one would carry the converters of an earlier
            // test's disposed IDataProtector. The protected-path test is the first to call Protect on non-null values.
            opts.ReplaceService<IModelCacheKeyFactory, NonCachingModelCacheKeyFactory>();
        });
        services.AddAutoMapper(typeof(UserRepository));

        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<DatabaseContext>();
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Commit_ProtectionDisabled_WritesKeyAndMasterPasswordUnchanged()
    {
        CommitUser(disableDataProtection: true);

        Assert.Equal(_key, ReadRawKey());
        Assert.Equal(_masterPassword, ReadRawMasterPassword());
    }

    [Fact]
    public void Commit_ProtectionEnabled_WritesKeyAndMasterPasswordWithPrefix()
    {
        CommitUser(disableDataProtection: false);

        Assert.StartsWith(_dataProtectionPrefix, ReadRawKey());
        Assert.StartsWith(_dataProtectionPrefix, ReadRawMasterPassword());
    }

    private void CommitUser(bool disableDataProtection)
    {
        var context = new SeederContext(_provider);
        context.Users.Add(new User
        {
            Id = CombGuid.Generate(),
            Email = $"bulk-committer-{Guid.NewGuid():N}@bw.example",
            SecurityStamp = Guid.NewGuid().ToString(),
            ApiKey = "test-api-key",
            Key = _key,
            MasterPassword = _masterPassword,
        });

        new BulkCommitter(_db, _provider.GetRequiredService<IMapper>(), disableDataProtection).Commit(context);
    }

    private string ReadRawKey() =>
        _db.Database.SqlQueryRaw<string>("SELECT \"Key\" AS \"Value\" FROM \"User\"").Single();

    private string ReadRawMasterPassword() =>
        _db.Database.SqlQueryRaw<string>("SELECT \"MasterPassword\" AS \"Value\" FROM \"User\"").Single();

    private sealed class NonCachingModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => new object();
    }
}
