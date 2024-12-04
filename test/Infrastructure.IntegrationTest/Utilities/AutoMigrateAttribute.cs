
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.IntegrationTest.Utilities;

public class AutoMigrateAttribute : TestCustomizerAttribute
{
    public AutoMigrateAttribute(string? migrationName = null)
    {
        MigrationName = migrationName;
    }

    public string? MigrationName { get; }

    public override Task CustomizeAsync(CustomizationContext customizationContext)
    {
        // Add migration services
        var database = customizationContext.Database;

        if (database.Type == SupportedDatabaseProviders.SqlServer && !database.UseEf)
        {
            // Add migrator service
        }
        else
        {
            // Add migrator service
        }

        // Build services provider early and run migrations
        var sp = customizationContext.Services.BuildServiceProvider();
        var migrator = sp.GetRequiredService<IMigrationTesterService>();
        migrator.ApplyMigration()
    }
}
