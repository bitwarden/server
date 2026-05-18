using Bit.Core.Repositories;

namespace Bit.Core.Auth.Sso;

/// <summary>
/// TODO : PM-28846 review data structures as they relate to this query
/// Query to retrieve the SSO organization identifier that a user is a member of.
/// </summary>
public class UserSsoOrganizationIdentifierQuery(
    IOrganizationUserRepository _organizationUserRepository,
    IOrganizationRepository _organizationRepository) : IUserSsoOrganizationIdentifierQuery
{
    /// <inheritdoc />
    public async Task<string?> GetSsoOrganizationIdentifierAsync(Guid userId)
    {
        var organizationUsers = await _organizationUserRepository.GetManyByUserAsync(userId);

        if (organizationUsers.Count != 1)
        {
            return null;
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationUsers.Single().OrganizationId);

        return organization?.Identifier;
    }
}
