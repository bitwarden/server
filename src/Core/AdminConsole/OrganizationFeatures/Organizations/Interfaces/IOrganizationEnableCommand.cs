namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationEnableCommand
{
    /// <summary>
    /// Enables an organization if it is currently disabled.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to enable.</param>
    Task EnableAsync(Guid organizationId);

    /// <summary>
    /// Enables an organization with a specified expiration date if it is currently disabled and has a gateway configured.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to enable.</param>
    /// <param name="expirationDate">The optional expiration date when the organization's subscription will expire.</param>
    Task EnableAsync(Guid organizationId, DateTime? expirationDate);
}
