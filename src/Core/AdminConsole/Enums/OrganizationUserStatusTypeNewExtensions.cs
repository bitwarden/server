using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Enums;

public static class OrganizationUserStatusTypeNewExtensions
{
    public static OrganizationUserStatusType ToOrganizationUserStatusType(this OrganizationUserStatusTypeNew status)
        => status switch
        {
            OrganizationUserStatusTypeNew.Invited => OrganizationUserStatusType.Invited,
            OrganizationUserStatusTypeNew.Accepted => OrganizationUserStatusType.Accepted,
            OrganizationUserStatusTypeNew.Confirmed => OrganizationUserStatusType.Confirmed,
            OrganizationUserStatusTypeNew.Staged => OrganizationUserStatusType.Staged,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status,
                "Unknown OrganizationUserStatusTypeNew value."),
        };

    /// <summary>
    /// Converts an active status to its <see cref="OrganizationUserStatusTypeNew"/> equivalent.
    /// Throws for <see cref="OrganizationUserStatusType.Revoked"/>, which has no equivalent.
    /// </summary>
    public static OrganizationUserStatusTypeNew ToOrganizationUserStatusTypeNew(this OrganizationUserStatusType status)
        => status switch
        {
            OrganizationUserStatusType.Invited => OrganizationUserStatusTypeNew.Invited,
            OrganizationUserStatusType.Accepted => OrganizationUserStatusTypeNew.Accepted,
            OrganizationUserStatusType.Confirmed => OrganizationUserStatusTypeNew.Confirmed,
            OrganizationUserStatusType.Staged => OrganizationUserStatusTypeNew.Staged,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status,
                "OrganizationUserStatusType value has no OrganizationUserStatusTypeNew equivalent."),
        };
}
