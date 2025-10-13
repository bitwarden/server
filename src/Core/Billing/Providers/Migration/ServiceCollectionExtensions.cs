using Bit.Core.Billing.Providers.Migration.Services;
using Bit.Core.Billing.Providers.Migration.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Billing.Providers.Migration;

public static class ServiceCollectionExtensions
{
    public static void AddProviderMigration(this IServiceCollection services)
    {
        services.AddTransient<IMigrationTrackerCache, MigrationTrackerDistributedCache>();
        services.AddTransient<IOrganizationMigrator, OrganizationMigrator>();
        services.AddTransient<IProviderMigrator, ProviderMigrator>();
    }
}
