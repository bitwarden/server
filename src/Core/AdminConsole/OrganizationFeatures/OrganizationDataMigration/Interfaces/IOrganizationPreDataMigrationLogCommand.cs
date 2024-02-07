namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDataMigration.Interfaces;

public interface IOrganizationPreDataMigrationLogCommand
{
    Task LogAsync(Guid organizationId);
}
