namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

/// <summary>
/// A data migration's hook into the chain tests, which find it by reflection. It seeds rows in their pre-migration
/// shape at the migration's marker and checks them after the migration runs, writing raw SQL against the schema as
/// it is there.
/// </summary>
public interface IDataMigrationTest
{
    string Name { get; }

    Task SeedAsync(IServiceProvider services);

    Task AssertMigratedAsync(IServiceProvider services);

    /// <summary>Checks the migrated rows again once every later schema migration has applied.</summary>
    Task AssertLatestAsync(IServiceProvider services);
}
