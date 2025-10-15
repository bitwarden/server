using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Migration;
using Bit.Seeder.Recipes;
using CommandDotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.DbSeederUtility;

public class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>()
            .Run(args);
    }

    [Command("organization", Description = "Seed an organization and organization users")]
    public void Organization(
        [Option('n', "Name", Description = "Name of organization")]
        string name,
        [Option('u', "users", Description = "Number of users to generate")]
        int users,
        [Option('d', "domain", Description = "Email domain for users")]
        string domain
    )
    {
        var services = new ServiceCollection();
        ServiceCollectionExtension.ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var db = scopedServices.GetRequiredService<DatabaseContext>();

        var recipe = new OrganizationWithUsersRecipe(db);
        recipe.Seed(name, users, domain);
    }

    [Command("discover", Description = "Discover and analyze tables in source database")]
    public void Discover(
        [Option("startssh", Description = "Start SSH tunnel before operation")]
        bool startSsh = false
    )
    {
        var config = MigrationSettingsFactory.MigrationConfig;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var recipe = new CsvMigrationRecipe(config, loggerFactory);

        if (startSsh && !recipe.StartSshTunnel(force: true))
        {
            Console.WriteLine("Failed to start SSH tunnel");
            return;
        }

        var success = recipe.DiscoverAndAnalyzeTables();

        if (startSsh)
        {
            recipe.StopSshTunnel();
        }

        if (!success)
        {
            Console.WriteLine("Discovery failed");
        }
    }

    [Command("export", Description = "Export tables from source database to CSV files")]
    public void Export(
        [Option("include-tables", Description = "Comma-separated list of tables to include")]
        string? includeTables = null,
        [Option("exclude-tables", Description = "Comma-separated list of tables to exclude")]
        string? excludeTables = null,
        [Option("startssh", Description = "Start SSH tunnel before operation")]
        bool startSsh = false
    )
    {
        var config = MigrationSettingsFactory.MigrationConfig;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var recipe = new CsvMigrationRecipe(config, loggerFactory);

        TableFilter? tableFilter = null;
        var includeList = TableFilter.ParseTableList(includeTables);
        var excludeList = TableFilter.ParseTableList(excludeTables);

        if (includeList.Count > 0 || excludeList.Count > 0)
        {
            tableFilter = new TableFilter(
                includeList.Count > 0 ? includeList : null,
                excludeList.Count > 0 ? excludeList : null,
                null,
                loggerFactory.CreateLogger<TableFilter>());
        }

        if (startSsh && !recipe.StartSshTunnel(force: true))
        {
            Console.WriteLine("Failed to start SSH tunnel");
            return;
        }

        var success = recipe.ExportAllTables(tableFilter);

        if (startSsh)
        {
            recipe.StopSshTunnel();
        }

        if (!success)
        {
            Console.WriteLine("Export failed");
        }
    }

    [Command("import", Description = "Import CSV files to destination database")]
    public void Import(
        [Operand(Description = "Database type (postgres, mariadb, sqlite, sqlserver)")]
        string database,
        [Option("create-tables", Description = "Create tables if they don't exist")]
        bool createTables = false,
        [Option("clear-existing", Description = "Clear existing data before import")]
        bool clearExisting = false,
        [Option("verify", Description = "Verify import after completion")]
        bool verify = false,
        [Option("include-tables", Description = "Comma-separated list of tables to include")]
        string? includeTables = null,
        [Option("exclude-tables", Description = "Comma-separated list of tables to exclude")]
        string? excludeTables = null,
        [Option("batch-size", Description = "Number of rows per batch")]
        int? batchSize = null
    )
    {
        var config = MigrationSettingsFactory.MigrationConfig;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var recipe = new CsvMigrationRecipe(config, loggerFactory);

        TableFilter? tableFilter = null;
        var includeList = TableFilter.ParseTableList(includeTables);
        var excludeList = TableFilter.ParseTableList(excludeTables);

        if (includeList.Count > 0 || excludeList.Count > 0)
        {
            tableFilter = new TableFilter(
                includeList.Count > 0 ? includeList : null,
                excludeList.Count > 0 ? excludeList : null,
                null,
                loggerFactory.CreateLogger<TableFilter>());
        }

        var success = recipe.ImportToDatabase(database, createTables, clearExisting, tableFilter, batchSize);

        if (verify && success)
        {
            Console.WriteLine("\nRunning verification...");
            var verifySuccess = recipe.VerifyImport(database, tableFilter);
            if (!verifySuccess)
            {
                Console.WriteLine("Import succeeded but verification found issues");
            }
        }

        if (!success)
        {
            Console.WriteLine("Import failed");
        }
    }

    [Command("verify", Description = "Verify import by comparing CSV row counts with database row counts")]
    public void Verify(
        [Operand(Description = "Database type (postgres, mariadb, sqlite, sqlserver)")]
        string database,
        [Option("include-tables", Description = "Comma-separated list of tables to include")]
        string? includeTables = null,
        [Option("exclude-tables", Description = "Comma-separated list of tables to exclude")]
        string? excludeTables = null
    )
    {
        var config = MigrationSettingsFactory.MigrationConfig;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var recipe = new CsvMigrationRecipe(config, loggerFactory);

        TableFilter? tableFilter = null;
        var includeList = TableFilter.ParseTableList(includeTables);
        var excludeList = TableFilter.ParseTableList(excludeTables);

        if (includeList.Count > 0 || excludeList.Count > 0)
        {
            tableFilter = new TableFilter(
                includeList.Count > 0 ? includeList : null,
                excludeList.Count > 0 ? excludeList : null,
                null,
                loggerFactory.CreateLogger<TableFilter>());
        }

        var success = recipe.VerifyImport(database, tableFilter);

        if (!success)
        {
            Console.WriteLine("Verification failed");
        }
    }

    [Command("test-connection", Description = "Test connection to a specific database")]
    public void TestConnection(
        [Operand(Description = "Database type (postgres, mariadb, sqlite, sqlserver)")]
        string database
    )
    {
        var config = MigrationSettingsFactory.MigrationConfig;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var recipe = new CsvMigrationRecipe(config, loggerFactory);

        var success = recipe.TestConnection(database);

        if (!success)
        {
            Console.WriteLine($"Connection to {database} failed");
        }
    }
}
