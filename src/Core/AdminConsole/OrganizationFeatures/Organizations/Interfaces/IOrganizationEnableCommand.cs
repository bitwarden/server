namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationEnableCommand
{
    /// <summary>
    /// Enables an organization that is currently disabled and has a gateway configured.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to enable.</param>
    /// <param name="expirationDate">When provided, sets the date the organization's subscription will expire. If not provided, no expiration date will be set.</param>
    Task EnableAsync(Guid organizationId, DateTime? expirationDate = null);
}
