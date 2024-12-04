using Bit.Core.Enums;
using Microsoft.Extensions.Configuration;
using Xunit.v3;
using Xunit.Sdk;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public record Database
{
    public string? Name { get; set; }
    public SupportedDatabaseProviders Type { get; set; }
    public string? ConnectionString { get; set; }
    public bool UseEf { get; set; }
    public bool Enabled { get; set; } = true;
}

internal class TypedConfig
{
    public Database[] Databases { get; set; } = default!;
}

public class DatabaseStartup : ITestPipelineStartup
{
    public static IReadOnlyList<Database>? Databases { get; private set; }

    public ValueTask StartAsync(IMessageSink diagnosticMessageSink)
    {
        List<Database> unconfiguredDatabases =
        [
            new Database
            {
                Type = SupportedDatabaseProviders.SqlServer,
                Enabled = false,
                Name = "Unconfigured",
            },
            new Database
            {
                Type = SupportedDatabaseProviders.MySql,
                Enabled = false,
                Name = "Unconfigured",
            },
            new Database
            {
                Type = SupportedDatabaseProviders.Postgres,
                Enabled = false,
                Name = "Unconfigured",
            },
            new Database
            {
                Type = SupportedDatabaseProviders.Sqlite,
                Enabled = false,
                Name = "Unconfigured",
            },
        ];

        // Do startup things
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseStartup>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("BW_TEST_")
            .AddCommandLine(Environment.GetCommandLineArgs())
            .Build();

        var typedConfig = configuration.Get<TypedConfig>();


        if (typedConfig is not { Databases: var databases })
        {
            Databases = unconfiguredDatabases;
            return ValueTask.CompletedTask;
        }

        var allDatabases = new List<Database>();
        foreach (var database in databases)
        {
            unconfiguredDatabases.RemoveAll(db => db.Type == database.Type);
            allDatabases.Add(database);
        }

        // Add entry for all still unconfigured database types
        allDatabases.AddRange(unconfiguredDatabases);

        Databases = allDatabases;

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        return ValueTask.CompletedTask;
    }
}
