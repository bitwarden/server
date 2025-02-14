namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

/// <summary>
/// Command interface for disabling organizations.
/// </summary>
public interface IOrganizationDisableCommand
{
    /// <summary>
    /// Disables an organization with an optional expiration date.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to disable.</param>
    /// <param name="expirationDate">Optional date when the disable status should expire.</param>
    Task DisableAsync(Guid organizationId, DateTime? expirationDate);
}
