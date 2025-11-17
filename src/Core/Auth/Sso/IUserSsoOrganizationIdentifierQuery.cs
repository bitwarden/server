using Bit.Core.Entities;

namespace Bit.Core.Auth.Sso;

/// <summary>
/// Query to retrieve the SSO organization identifier that a user is a confirmed member of.
/// </summary>
public interface IUserSsoOrganizationIdentifierQuery
{
    /// <summary>
    /// Retrieves the SSO organization identifier for a confirmed organization user.
    /// If there is more than one organization a User is associated with, we return null. If there are more than one
    /// organization there is no way to know which organization the user wishes to authenticate with.
    /// Owners and Admins who are not subject to the SSO required policy cannot utilize this flow, since they may have
    /// multiple organizations with different SSO configurations.
    /// </summary>
    /// <param name="userId">The ID of the <see cref="User"/> to retrieve the SSO organization for. _Not_ an <see cref="OrganizationUser"/>.</param>
    /// <returns>
    /// The organization identifier if the user is a confirmed member of an organization with SSO configured,
    /// otherwise null
    /// </returns>
    Task<string?> GetSsoOrganizationIdentifierAsync(Guid userId);
}
