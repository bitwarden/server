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

    [Fact]
    public void CreateInvited_ReturnsInvitedShape()
    {
        var organizationId = Guid.NewGuid();

        var orgUser = OrganizationUser.CreateInvited(organizationId, "Invitee@Example.com", OrganizationUserType.Admin,
            "external-id");

        Assert.NotEqual(Guid.Empty, orgUser.Id);
        Assert.Equal(organizationId, orgUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal(OrganizationUserType.Admin, orgUser.Type);
        Assert.Equal("invitee@example.com", orgUser.Email);
        Assert.Null(orgUser.UserId);
        Assert.Null(orgUser.Key);
        Assert.Null(orgUser.StatusNew);
        Assert.Equal("external-id", orgUser.ExternalId);
    }

    [Fact]
    public void CreateInvited_NoExternalId_LeavesExternalIdNull()
    {
        var orgUser = OrganizationUser.CreateInvited(Guid.NewGuid(), "invitee@example.com", OrganizationUserType.User);

        Assert.Null(orgUser.ExternalId);
    }

    [Fact]
    public void CreateAccepted_ReturnsAcceptedShape()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var orgUser = OrganizationUser.CreateAccepted(organizationId, userId, OrganizationUserType.User, "external-id");

        Assert.NotEqual(Guid.Empty, orgUser.Id);
        Assert.Equal(organizationId, orgUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.Status);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal(userId, orgUser.UserId);
        Assert.Null(orgUser.Email);
        Assert.Null(orgUser.Key);
        Assert.Null(orgUser.StatusNew);
        Assert.Equal("external-id", orgUser.ExternalId);
    }

    [Fact]
    public void CreateConfirmed_ReturnsConfirmedShape()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var orgUser = OrganizationUser.CreateConfirmed(organizationId, userId, "org-key", OrganizationUserType.Owner,
            "external-id");

        Assert.NotEqual(Guid.Empty, orgUser.Id);
        Assert.Equal(organizationId, orgUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Equal(OrganizationUserType.Owner, orgUser.Type);
        Assert.Equal(userId, orgUser.UserId);
        Assert.Equal("org-key", orgUser.Key);
        Assert.Null(orgUser.Email);
        Assert.Null(orgUser.StatusNew);
        Assert.Equal("external-id", orgUser.ExternalId);
    }

    [Fact]
    public void CreateStaged_ReturnsStagedShape()
    {
        var organizationId = Guid.NewGuid();

        var orgUser = OrganizationUser.CreateStaged(organizationId, "Staged@Example.com", "external-id",
            OrganizationUserType.User);

        Assert.NotEqual(Guid.Empty, orgUser.Id);
        Assert.Equal(organizationId, orgUser.OrganizationId);
        Assert.Equal(OrganizationUserStatusType.Staged, orgUser.Status);
        Assert.Equal(OrganizationUserType.User, orgUser.Type);
        Assert.Equal("staged@example.com", orgUser.Email);
        Assert.Equal("external-id", orgUser.ExternalId);
        Assert.Null(orgUser.UserId);
        Assert.Null(orgUser.Key);
        Assert.Null(orgUser.StatusNew);
    }

    [Fact]
    public void Create_EachCallGeneratesDistinctId()
    {
        var first = OrganizationUser.CreateInvited(Guid.NewGuid(), "a@example.com", OrganizationUserType.User);
        var second = OrganizationUser.CreateInvited(Guid.NewGuid(), "b@example.com", OrganizationUserType.User);

        Assert.NotEqual(first.Id, second.Id);
    }

    public static TheoryData<OrganizationUser, OrganizationUserStatusType> FactoryArrangements => new()
    {
        { OrganizationUser.CreateInvited(Guid.NewGuid(), "invitee@example.com", OrganizationUserType.User), OrganizationUserStatusType.Invited },
        { OrganizationUser.CreateAccepted(Guid.NewGuid(), Guid.NewGuid(), OrganizationUserType.User), OrganizationUserStatusType.Accepted },
        { OrganizationUser.CreateConfirmed(Guid.NewGuid(), Guid.NewGuid(), "org-key", OrganizationUserType.User), OrganizationUserStatusType.Confirmed },
    };

    /// <summary>
    /// Restore without a StatusNew snapshot infers status from the row's shape, so factory rows must round-trip.
    /// </summary>
    [Theory]
    [MemberData(nameof(FactoryArrangements))]
    public void GetPriorActiveOrganizationUserStatusType_FactoryArrangement_ResolvesToCreatedStatus(
        OrganizationUser orgUser,
        OrganizationUserStatusType expected)
    {
        Assert.Equal(expected, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Fact]
    public void Invite_Staged_MovesToInvited()
    {
        var orgUser = OrganizationUser.CreateStaged(Guid.NewGuid(), "staged@example.com", "external-id",
            OrganizationUserType.User);

        orgUser.Invite();

        Assert.Equal(OrganizationUserStatusType.Invited, orgUser.Status);
        Assert.Equal("staged@example.com", orgUser.Email);
        Assert.Null(orgUser.UserId);
        Assert.Null(orgUser.Key);
    }

    [Fact]
    public void Accept_Invited_MovesToAccepted()
    {
        var userId = Guid.NewGuid();
        var orgUser = OrganizationUser.CreateInvited(Guid.NewGuid(), "invitee@example.com", OrganizationUserType.User);
        orgUser.InviteReminderDueDate = DateTime.UtcNow;
        orgUser.InviteRemindersSentCount = 1;

        orgUser.Accept(userId);

        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.Status);
        Assert.Equal(userId, orgUser.UserId);
        Assert.Null(orgUser.Email);
        Assert.Null(orgUser.Key);
        Assert.Null(orgUser.InviteReminderDueDate);
        Assert.Null(orgUser.InviteRemindersSentCount);
    }

    [Fact]
    public void Confirm_Accepted_MovesToConfirmed()
    {
        var userId = Guid.NewGuid();
        var orgUser = OrganizationUser.CreateAccepted(Guid.NewGuid(), userId, OrganizationUserType.User);

        orgUser.Confirm("org-key");

        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Equal("org-key", orgUser.Key);
        Assert.Equal(userId, orgUser.UserId);
        Assert.Null(orgUser.Email);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Confirm_InvalidKey_Throws(string? key)
    {
        var orgUser = OrganizationUser.CreateAccepted(Guid.NewGuid(), Guid.NewGuid(), OrganizationUserType.User);

        Assert.ThrowsAny<ArgumentException>(() => orgUser.Confirm(key!));
        Assert.Equal(OrganizationUserStatusType.Accepted, orgUser.Status);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited, OrganizationUserStatusTypeNew.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted, OrganizationUserStatusTypeNew.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed, OrganizationUserStatusTypeNew.Confirmed)]
    [InlineData(OrganizationUserStatusType.Staged, OrganizationUserStatusTypeNew.Staged)]
    public void Revoke_ActiveStatus_MovesToRevokedAndSnapshotsStatus(
        OrganizationUserStatusType status,
        OrganizationUserStatusTypeNew expectedStatusNew)
    {
        var orgUser = new OrganizationUser { Status = status };

        orgUser.Revoke(RevocationReason.Manual);

        Assert.Equal(OrganizationUserStatusType.Revoked, orgUser.Status);
        Assert.Equal(expectedStatusNew, orgUser.StatusNew);
        Assert.Equal(RevocationReason.Manual, orgUser.RevocationReason);
    }

    [Fact]
    public void Revoke_KeepsInviteReminders()
    {
        var dueDate = DateTime.UtcNow;
        var orgUser = OrganizationUser.CreateInvited(Guid.NewGuid(), "invitee@example.com", OrganizationUserType.User);
        orgUser.InviteReminderDueDate = dueDate;
        orgUser.InviteRemindersSentCount = 1;

        orgUser.Revoke(RevocationReason.Manual);

        Assert.Equal(dueDate, orgUser.InviteReminderDueDate);
        Assert.Equal((byte)1, orgUser.InviteRemindersSentCount);
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsAndPreservesSnapshot()
    {
        var orgUser = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Revoked,
            StatusNew = OrganizationUserStatusTypeNew.Confirmed,
            RevocationReason = RevocationReason.Manual,
        };

        Assert.Throws<InvalidOperationException>(() => orgUser.Revoke(RevocationReason.SingleOrgPolicyNonCompliance));
        Assert.Equal(OrganizationUserStatusTypeNew.Confirmed, orgUser.StatusNew);
        Assert.Equal(RevocationReason.Manual, orgUser.RevocationReason);
    }

    [Theory]
    [InlineData(OrganizationUserStatusTypeNew.Invited, OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusTypeNew.Accepted, OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusTypeNew.Confirmed, OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusTypeNew.Staged, OrganizationUserStatusType.Staged)]
    public void Restore_Revoked_MovesToStatusNewAndClearsRevocation(
        OrganizationUserStatusTypeNew statusNew,
        OrganizationUserStatusType expected)
    {
        var orgUser = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Revoked,
            StatusNew = statusNew,
            RevocationReason = RevocationReason.Manual,
        };

        orgUser.Restore();

        Assert.Equal(expected, orgUser.Status);
        Assert.Null(orgUser.StatusNew);
        Assert.Null(orgUser.RevocationReason);
    }

    [Fact]
    public void Restore_RevokedWithoutStatusNew_InfersStatusFromArrangement()
    {
        var orgUser = new OrganizationUser
        {
            Status = OrganizationUserStatusType.Revoked,
            UserId = Guid.NewGuid(),
            Key = "org-key",
            StatusNew = null,
            RevocationReason = RevocationReason.Unknown,
        };

        orgUser.Restore();

        Assert.Equal(OrganizationUserStatusType.Confirmed, orgUser.Status);
        Assert.Null(orgUser.RevocationReason);
    }

    public static TheoryData<OrganizationUser, OrganizationUserStatusType> TransitionedArrangements()
    {
        var invited = OrganizationUser.CreateStaged(Guid.NewGuid(), "staged@example.com", "external-id",
            OrganizationUserType.User);
        invited.Invite();

        var accepted = OrganizationUser.CreateInvited(Guid.NewGuid(), "invitee@example.com", OrganizationUserType.User);
        accepted.Accept(Guid.NewGuid());

        var confirmed = OrganizationUser.CreateAccepted(Guid.NewGuid(), Guid.NewGuid(), OrganizationUserType.User);
        confirmed.Confirm("org-key");

        var restored = OrganizationUser.CreateConfirmed(Guid.NewGuid(), Guid.NewGuid(), "org-key",
            OrganizationUserType.User);
        restored.Revoke(RevocationReason.Manual);
        restored.Restore();

        return new()
        {
            { invited, OrganizationUserStatusType.Invited },
            { accepted, OrganizationUserStatusType.Accepted },
            { confirmed, OrganizationUserStatusType.Confirmed },
            { restored, OrganizationUserStatusType.Confirmed },
        };
    }

    /// <summary>
    /// Restore without a StatusNew snapshot infers status from the row's shape, so transitioned rows must round-trip.
    /// </summary>
    [Theory]
    [MemberData(nameof(TransitionedArrangements))]
    public void GetPriorActiveOrganizationUserStatusType_TransitionedArrangement_ResolvesToCurrentStatus(
        OrganizationUser orgUser,
        OrganizationUserStatusType expected)
    {
        Assert.Equal(expected, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Staged)]
    public void GetPriorActiveOrganizationUserStatusType_AfterRevoke_ResolvesToPreRevokeStatus(
        OrganizationUserStatusType status)
    {
        var orgUser = new OrganizationUser { Status = status };

        orgUser.Revoke(RevocationReason.Manual);

        Assert.Equal(status, orgUser.GetPriorActiveOrganizationUserStatusType());
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public void Invite_NotStaged_Throws(OrganizationUserStatusType status)
    {
        var orgUser = new OrganizationUser { Status = status };

        Assert.Throws<InvalidOperationException>(() => orgUser.Invite());
        Assert.Equal(status, orgUser.Status);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Staged)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public void Accept_NotInvited_Throws(OrganizationUserStatusType status)
    {
        var orgUser = new OrganizationUser { Status = status };

        Assert.Throws<InvalidOperationException>(() => orgUser.Accept(Guid.NewGuid()));
        Assert.Equal(status, orgUser.Status);
        Assert.Null(orgUser.UserId);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Staged)]
    [InlineData(OrganizationUserStatusType.Revoked)]
    public void Confirm_NotAccepted_Throws(OrganizationUserStatusType status)
    {
        var orgUser = new OrganizationUser { Status = status };

        Assert.Throws<InvalidOperationException>(() => orgUser.Confirm("org-key"));
        Assert.Equal(status, orgUser.Status);
        Assert.Null(orgUser.Key);
    }

    [Theory]
    [InlineData(OrganizationUserStatusType.Invited)]
    [InlineData(OrganizationUserStatusType.Accepted)]
    [InlineData(OrganizationUserStatusType.Confirmed)]
    [InlineData(OrganizationUserStatusType.Staged)]
    public void Restore_NotRevoked_Throws(OrganizationUserStatusType status)
    {
        var orgUser = new OrganizationUser { Status = status };

        Assert.Throws<InvalidOperationException>(() => orgUser.Restore());
        Assert.Equal(status, orgUser.Status);
    }
}
