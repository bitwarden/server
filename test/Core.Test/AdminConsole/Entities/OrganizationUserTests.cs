using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Core.Test.AdminConsole.Entities;

public class OrganizationUserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IsValidResetPasswordKey_InvalidKeys_ReturnsFalse(string? resetPasswordKey)
    {
        Assert.False(OrganizationUser.IsValidResetPasswordKey(resetPasswordKey));
    }

    [Fact]
    public void IsValidResetPasswordKey_ValidKey_ReturnsTrue()
    {
        Assert.True(OrganizationUser.IsValidResetPasswordKey("validKey"));
    }

    [Fact]
    public void IsEnrolledInAccountRecovery_NullKey_ReturnsFalse()
    {
        var orgUser = new OrganizationUser { ResetPasswordKey = null };

        Assert.False(orgUser.IsEnrolledInAccountRecovery());
    }

    [Fact]
    public void IsEnrolledInAccountRecovery_ValidKey_ReturnsTrue()
    {
        var orgUser = new OrganizationUser { ResetPasswordKey = "validKey" };

        Assert.True(orgUser.IsEnrolledInAccountRecovery());
    }

    [Theory]
    [InlineData(OrganizationUserStatusTypeNew.Invited, OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusTypeNew.Accepted, OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusTypeNew.Confirmed, OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusTypeNew.Staged, OrganizationUserStatusType.Staged)]
    public void GetPriorActiveOrganizationUserStatusType_StatusNewPopulated_ReturnsStatusNew_RegardlessOfArrangement(
        OrganizationUserStatusTypeNew statusNew,
        OrganizationUserStatusType expected)
    {
        var orgUser = new OrganizationUser
        {
            UserId = Guid.NewGuid(),
            Email = null,
            Key = "some-key",
            StatusNew = statusNew,
        };

        Assert.Equal(expected, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Fact]
    public void GetPriorActiveOrganizationUserStatusType_StatusNewNull_InvitedArrangement_ReturnsInvited()
    {
        var orgUser = new OrganizationUser
        {
            UserId = null,
            Email = "invitee@example.com",
            Key = null,
            StatusNew = null,
        };

        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Fact]
    public void GetPriorActiveOrganizationUserStatusType_StatusNewNull_AcceptedArrangement_ReturnsAccepted()
    {
        var orgUser = new OrganizationUser
        {
            UserId = Guid.NewGuid(),
            Email = null,
            Key = null,
            StatusNew = null,
        };

        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Fact]
    public void GetPriorActiveOrganizationUserStatusType_StatusNewNull_ConfirmedArrangement_ReturnsConfirmed()
    {
        var orgUser = new OrganizationUser
        {
            UserId = Guid.NewGuid(),
            Email = null,
            Key = "some-key",
            StatusNew = null,
        };

        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.GetPriorActiveOrganizationUserStatusType());
    }
}
