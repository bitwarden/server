using System.Reflection;
using Bit.Core.Utilities;
using Bit.DataMigrations;
using Bit.Infrastructure.EntityFramework;
using Bit.Migrator;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

public class DataMigrationChainTests
{
    private static readonly Dictionary<string, IDataMigrationTest> _hooks = typeof(IDataMigrationTest).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(IDataMigrationTest)))
        .Select(t => (IDataMigrationTest)Activator.CreateInstance(t)!)
        .ToDictionary(h => h.Name);

    [Fact]
    public void EveryDataMigrationHasAMarkerInEveryChainAndATestHook()
    {
        var services = new ServiceCollection().AddLogging().AddDataProtection().Services.AddDataMigrations();
        using var provider = services.BuildServiceProvider();
        var registered = provider.GetServices<DataMigration>().Select(m => m.Name).Order().ToList();

        Assert.Equal(registered, _hooks.Keys.Order());
        var dbScripts = typeof(DbMigrator).Assembly.GetManifestResourceNames().Where(n => n.Contains(".DbScripts."));
        Assert.Equal(registered, Markers(dbScripts));
        foreach (var assembly in new[] { "MySqlMigrations", "PostgresMigrations", "SqliteMigrations" })
        {
            var ids = Assembly.Load(assembly).GetTypes().Select(t => t.GetCustomAttribute<MigrationAttribute>()?.Id).OfType<string>();
            Assert.Equal(registered, Markers(ids));
        }
    }

    [Theory, DatabaseData]
    public async Task Migrate_RunsEveryDataMigrationAtItsMarker(Database database)
    {
        await using var db = new MigrationTestDatabase(database);
        var runner = db.Services.GetRequiredService<IDataMigrationRunner>();
        var ran = new List<string>();

        var migrated = db.Migrate(name =>
        {
            var hook = _hooks[name];
            hook.SeedAsync(db.Services).GetAwaiter().GetResult();
            Assert.True(runner.RunToCompletionAsync(name).GetAwaiter().GetResult());
            hook.AssertMigratedAsync(db.Services).GetAwaiter().GetResult();
            ran.Add(name);
            return true;
        });

        Assert.True(migrated);
        Assert.Equal(runner.Names.Order(), ran.Order());
        foreach (var hook in _hooks.Values)
        {
            await hook.AssertLatestAsync(db.Services);
        }
    }

    private static List<string> Markers(IEnumerable<string> migrations) =>
        migrations.Select(DataMigrationMarkers.GetName).OfType<string>().Order().ToList();
}
