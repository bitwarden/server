using Bit.Core.Billing.Organizations.Helpers;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.Helpers;

public class PrivilegedControlsSeatOccupancyTests
{
    [Theory]
    [InlineData(OrganizationUserStatusType.Invited, true)]
    [InlineData(OrganizationUserStatusType.Accepted, true)]
    [InlineData(OrganizationUserStatusType.Confirmed, true)]
    [InlineData(OrganizationUserStatusType.Revoked, false)]
    [InlineData(OrganizationUserStatusType.Staged, false)]
    public void OccupiesSeat_TrueOnlyForInvitedAcceptedConfirmed(OrganizationUserStatusType status, bool expected)
    {
        Assert.Equal(expected, PrivilegedControlsSeatOccupancy.OccupiesSeat(status));
    }
}
