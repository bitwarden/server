using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Scim.Utilities;

public static class OrganizationUserUserDetailsExtensions
{
    /// <summary>
    /// The member's SCIM userName: the one the identity provider sent, or the email for members
    /// provisioned before it was stored.
    /// </summary>
    public static string ScimUserName(this OrganizationUserUserDetails orgUser) =>
        orgUser.ExternalName ?? orgUser.Email;

    /// <summary>
    /// userName is not case-exact (RFC 7643 §4.1.1).
    /// </summary>
    public static bool HasScimUserName(this OrganizationUserUserDetails orgUser, string? userName) =>
        string.Equals(orgUser.ScimUserName(), userName, StringComparison.OrdinalIgnoreCase);
}
