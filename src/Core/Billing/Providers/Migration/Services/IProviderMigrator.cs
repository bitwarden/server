using Bit.Core.Billing.Providers.Migration.Models;

namespace Bit.Core.Billing.Providers.Migration.Services;

public interface IProviderMigrator
{
    Task Migrate(Guid providerId);

    Task<ProviderMigrationResult> GetResult(Guid providerId);
}
