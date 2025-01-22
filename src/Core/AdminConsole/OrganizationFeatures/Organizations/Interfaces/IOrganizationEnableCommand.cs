namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationEnableCommand
{
    Task EnableAsync(Guid organizationId);
    Task EnableAsync(Guid organizationId, DateTime? expirationDate);
}
