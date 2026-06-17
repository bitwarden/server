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
            _ => throw new ArgumentOutOfRangeException(nameof(status), status,
                "Unknown OrganizationUserStatusTypeNew value."),
        };
}
