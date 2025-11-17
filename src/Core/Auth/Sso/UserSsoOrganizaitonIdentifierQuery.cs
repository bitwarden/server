using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.Sso;

/// <summary>
/// Query to retrieve the SSO organization identifier that a user is a confirmed member of.
/// </summary>
public class UserSsoOrganizationIdentifierQuery(
    IOrganizationUserRepository _organizationUserRepository,
    IOrganizationRepository _organizationRepository) : IUserSsoOrganizationIdentifierQuery
{
    /// <inheritdoc />
    public async Task<string?> GetSsoOrganizationIdentifierAsync(Guid userId)
    {
        // Get all confirmed organization memberships for the user
        var organizationUsers = await _organizationUserRepository.GetManyByUserAsync(userId);

        // we can only confidently return the correct SsoOrganizationIdentifier if there is exactly one Organization.
        // The user must also be in the Confirmed status.
        var confirmedOrgUsers = organizationUsers.Where(ou => ou.Status == OrganizationUserStatusType.Confirmed);
        if (confirmedOrgUsers.Count() != 1)
        {
            return null;
        }

        var confirmedOrgUser = confirmedOrgUsers.Single();
        var organization = await _organizationRepository.GetByIdAsync(confirmedOrgUser.OrganizationId);

        if (organization == null)
        {
            return null;
        }

        return organization.Identifier;
    }
}
