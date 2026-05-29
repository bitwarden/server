using Bit.Core.PrivilegedAccessManagement.Engine;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleEngineTests
{
    [Fact]
    public void Check_CipherHasNoRule_Grants()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithNoRules();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_ActiveLeaseExists_GrantsWithoutCreatingAnother()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithActiveLease();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
        Assert.Equal(0, fixture.LeasesCreated);
    }

    [Fact]
    public void Check_ExpiredLeaseExists_IsIgnoredAndANewLeaseIsCreated()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithExpiredLease();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
        Assert.Equal(1, fixture.LeasesCreated);
    }

    [Fact]
    public void Check_ApprovalRequiredAndRequestNotApproved_ReturnsRequiresApproval()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.RequiresApproval, result.Outcome);
        Assert.True(fixture.RequestWasCreated);
    }

    [Fact]
    public void Check_ApprovalRequiredAndRequestApproved_Grants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval()
            .WithApprovedRequest();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_SingletonRequiredAndAnotherUserHoldsActiveLease_ReturnsDeniedSingletonHeld()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringSingleton()
            .WithActiveLeaseHeldBy(AccessRuleEngineFixture.AnotherUser);

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.SingletonHeld, result.Reason);
    }

    [Fact]
    public void Check_SingletonRequiredAndNoExistingLease_Grants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringSingleton();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_IpAddressOutsideRequiredCidr_ReturnsDeniedNotWithinIpRange()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("192.168.1.5");

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.NotWithinIpRange, result.Reason);
    }

    [Fact]
    public void Check_IpAddressWithinRequiredCidr_Grants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("10.0.0.5");

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_UnparseableCidrEntryIsSkipped_AndALaterMatchGrants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("not-a-cidr", "10.0.0.0/24")
            .FromIpAddress("10.0.0.5");

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_LeaseCreationFails_ReturnsLeaseCreationFailed()
    {
        var fixture = new AccessRuleEngineFixture()
            .WhereLeaseCreationFails();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.LeaseCreationFailed, result.Outcome);
    }

    [Fact]
    public void Check_PermissiveRuleAndNoExistingLease_GrantsForRequestingUser()
    {
        var fixture = new AccessRuleEngineFixture();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
        Assert.Equal(1, fixture.LeasesCreated);
    }

    [Fact]
    public void Check_OutsideTimeWindow_ReturnsDeniedNotWithinTimeWindow()
    {
        // The fixture's request time is 12:00 UTC, outside the 13:00-17:00 window
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToTimeWindow("UTC", new TimeOnly(13, 0), new TimeOnly(17, 0));

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.NotWithinTimeWindow, result.Reason);
    }

    [Fact]
    public void Check_ApprovalRequired_RemainsPendingUntilApproved_ThenGrants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval();

        // First request creates the pending approval request.
        var first = fixture.Check(fixture.Cipher);
        Assert.Equal(AccessOutcome.RequiresApproval, first.Outcome);
        Assert.True(fixture.RequestWasCreated);

        // A second request for the same cipher stays pending and does not create a duplicate.
        var second = fixture.Check(fixture.Cipher);
        Assert.Equal(AccessOutcome.RequiresApproval, second.Outcome);
        Assert.Equal(1, fixture.RequestsCreated);

        // The pending request is approved out of band.
        fixture.ApproveRequest();

        // A third request for the same cipher is now granted.
        var third = fixture.Check(fixture.Cipher);
        Assert.Equal(AccessOutcome.Granted, third.Outcome);
    }
}
