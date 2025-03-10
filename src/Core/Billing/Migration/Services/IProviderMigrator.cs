using Bit.Core.Billing.Migration.Models;

namespace Bit.Core.Billing.Migration.Services;

public interface IProviderMigrator
{
    Task Migrate(Guid providerId);

    Task<ProviderMigrationResult> GetResult(Guid providerId);
}
