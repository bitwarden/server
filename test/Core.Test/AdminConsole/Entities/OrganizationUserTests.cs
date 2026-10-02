using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Microsoft.Extensions.Time.Testing;
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

    /// <summary>
    /// The two entitlement flags are adjacent bool parameters, so a swapped call site would compile and
    /// silently grant the wrong one. Every combination is asserted to land in its own field.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void UpdateOrganizationUser_MapsEntitlementFlagsIndependently(bool accessSecretsManager, bool accessPam)
    {
        var orgUser = new OrganizationUser
        {
            Type = OrganizationUserType.User,
            AccessSecretsManager = !accessSecretsManager,
            AccessPam = !accessPam
        };

        orgUser.UpdateOrganizationUser(OrganizationUserType.Admin, null, accessSecretsManager, accessPam,
            TimeProvider.System);

        Assert.Equal(accessSecretsManager, orgUser.AccessSecretsManager);
        Assert.Equal(accessPam, orgUser.AccessPam);
        Assert.Equal(OrganizationUserType.Admin, orgUser.Type);
    }

    [Fact]
    public void UpdateOrganizationUser_SetsRevisionDateFromTimeProvider()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var orgUser = new OrganizationUser { Type = OrganizationUserType.User };

        orgUser.UpdateOrganizationUser(OrganizationUserType.User, null, false, false, timeProvider);

        Assert.Equal(now.UtcDateTime, orgUser.RevisionDate);
    }

    [Theory]
    [InlineData(OrganizationUserType.User)]
    [InlineData(OrganizationUserType.Admin)]
    [InlineData(OrganizationUserType.Owner)]
    public void UpdateOrganizationUser_ConvertedFromCustom_ClearsPermissions(OrganizationUserType newType)
    {
        var orgUser = new OrganizationUser { Type = OrganizationUserType.Custom };
        orgUser.SetPermissions(new Permissions { ManageUsers = true });

        orgUser.UpdateOrganizationUser(newType, new Permissions { ManageUsers = true }, false, false,
            TimeProvider.System);

        Assert.Null(orgUser.Permissions);
        Assert.Null(orgUser.GetPermissions());
    }

    [Fact]
    public void UpdateOrganizationUser_ConvertedToCustom_SetsPermissions()
    {
        var orgUser = new OrganizationUser { Type = OrganizationUserType.User };

        orgUser.UpdateOrganizationUser(OrganizationUserType.Custom, new Permissions { ManageUsers = true }, false,
            false, TimeProvider.System);

        Assert.True(orgUser.GetPermissions()!.ManageUsers);
    }
}
