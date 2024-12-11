namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IGetOrganizationUsersManagementStatusQuery
{
    /// <summary>
    /// Checks whether each user in the provided list of organization user IDs is managed by the specified organization.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to check against.</param>
    /// <param name="organizationUserIds">A list of OrganizationUserIds to be checked.</param>
    /// <remarks>
    /// A managed user is a user whose email domain matches one of the Organization's verified domains.
    /// The organization must be enabled and be on an Enterprise plan.
    /// </remarks>
    /// <returns>
    /// A dictionary containing the OrganizationUserId and a boolean indicating if the user is managed by the organization.
    /// </returns>
    Task<IDictionary<Guid, bool>> GetUsersOrganizationManagementStatusAsync(
        Guid organizationId,
        IEnumerable<Guid> organizationUserIds
    );
}
