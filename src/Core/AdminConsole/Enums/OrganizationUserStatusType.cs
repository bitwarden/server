using Bit.Core.Entities;

namespace Bit.Core.Enums;

/// <summary>
/// Represents the different stages of a member's lifecycle in an organization.
/// The <see cref="OrganizationUser"/> object is populated differently depending on their Status.
/// </summary>
public enum OrganizationUserStatusType : short
{
    /// <summary>
    /// The OrganizationUser entry only represents an invitation to join the organization. It is not linked to a
    /// specific User yet.
    /// </summary>
    Invited = 0,
    /// <summary>
    /// The User has accepted the invitation and linked their User account to the OrganizationUser entry.
    /// </summary>
    Accepted = 1,
    /// <summary>
    /// An administrator has granted the User access to the organization. This is the final step in the User becoming
    /// a "full" member of the organization, including a key exchange so that they can decrypt organization data.
    /// </summary>
    Confirmed = 2,
    /// <summary>
    /// The OrganizationUser has been revoked from the organization and cannot access organization data while in this state.
    /// </summary>
    /// <remarks>
    /// An OrganizationUser may move into this status from any other status, and will move back to their original status
    /// if restored. This allows an administrator to easily suspend and restore access without going through the
    /// Invite flow again.
    /// </remarks>
    Revoked = -1,
}
