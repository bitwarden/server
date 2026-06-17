using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.Enums;

/// <summary>
/// Represents the different stages of a member's lifecycle in an organization.
/// The <see cref="OrganizationUser"/> object is populated differently depending on their Status.
/// </summary>
/// <remarks>
/// This is effectively a v2 version of OrganizationUserStatusType that severs Revoked as a status type.
/// </remarks>
public enum OrganizationUserStatusTypeNew : short
{
    Invited = 0,
    Accepted = 1,
    Confirmed = 2,
}
