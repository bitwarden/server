using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.Billing.Providers.Migration.Services;

public interface IOrganizationMigrator
{
    Task Migrate(Guid providerId, Organization organization);
}
