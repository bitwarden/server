using Bit.Core.Enums;

namespace Bit.Core.Billing.Organizations.Helpers;

public static class PrivilegedControlsSeatOccupancy
{
    /// <summary>
    /// Whether a member with this status occupies a Privileged Controls seat once granted access.
    /// </summary>
    public static bool OccupiesSeat(OrganizationUserStatusType status) =>
        status is OrganizationUserStatusType.Invited
            or OrganizationUserStatusType.Accepted
            or OrganizationUserStatusType.Confirmed;
}
