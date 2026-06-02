using System.Net;
using Bit.Core.PrivilegedAccessManagement.Engine;
using Xunit;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleEngineTests
{
    // Check: access is granted only when the user already holds a valid, unexpired lease.

    [Fact]
    public void Check_NoLease_DeniesWithNoLease()
    {
        var fixture = new AccessRuleEngineFixture();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.NoLease, result.Reason);
    }

    [Fact]
    public void Check_ExpiredLease_DeniesWithInvalidLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithExpiredLease();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.InvalidLease, result.Reason);
    }

    [Fact]
    public void Check_ValidLease_Grants()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithActiveLease();

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Granted, result.Outcome);
    }

    [Fact]
    public void Check_ValidLeaseHeldByAnotherUser_DeniesWithNoLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithActiveLeaseHeldBy(AccessRuleEngineFixture.AnotherUser);

        var result = fixture.Check(fixture.Cipher);

        Assert.Equal(AccessOutcome.Denied, result.Outcome);
        Assert.Equal(DenyReason.NoLease, result.Reason);
    }

    // RequestAccess: create a pending request unless an active lease or pending request already exists.

    [Fact]
    public void RequestAccess_NoLeaseNoRequest_CreatesRequest()
    {
        var fixture = new AccessRuleEngineFixture();

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Created, result.Outcome);
        Assert.NotNull(result.Request);
        Assert.Equal(1, fixture.RequestsCreated);
    }

    [Fact]
    public void RequestAccess_CapturesRequestingUserSignals()
    {
        var fixture = new AccessRuleEngineFixture()
            .FromIpAddress("10.0.0.5");

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.NotNull(result.Request);
        Assert.Equal(AccessRuleEngineFixture.RequestingUser, result.Request.Username);
        Assert.Equal(IPAddress.Parse("10.0.0.5"), result.Request.Signals.IpAddress);
    }

    [Fact]
    public void RequestAccess_ActiveLeaseExists_FailsWithExistingLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithActiveLease();

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Failed, result.Outcome);
        Assert.Equal(RequestAccessFailReason.ExistingLease, result.FailReason);
        Assert.Equal(0, fixture.RequestsCreated);
    }

    [Fact]
    public void RequestAccess_ExpiredLeaseExists_CreatesRequest()
    {
        // An expired lease no longer grants access, so the user may request again.
        var fixture = new AccessRuleEngineFixture()
            .WithExpiredLease();

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Created, result.Outcome);
        Assert.Equal(1, fixture.RequestsCreated);
    }

    [Fact]
    public void RequestAccess_RequestAlreadyExists_FailsWithExistingRequest()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithPendingRequest();

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Failed, result.Outcome);
        Assert.Equal(RequestAccessFailReason.ExistingRequest, result.FailReason);
        Assert.Equal(0, fixture.RequestsCreated);
    }

    [Fact]
    public void RequestAccess_CalledTwice_SecondFailsWithExistingRequest()
    {
        var fixture = new AccessRuleEngineFixture();

        var first = fixture.RequestAccess(fixture.Cipher);
        var second = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Created, first.Outcome);
        Assert.Equal(RequestAccessOutcome.Failed, second.Outcome);
        Assert.Equal(RequestAccessFailReason.ExistingRequest, second.FailReason);
        Assert.Equal(1, fixture.RequestsCreated);
    }

    [Fact]
    public void RequestAccess_AnotherUserHoldsActiveLease_StillCreatesRequest()
    {
        // The singleton constraint is enforced when exchanging, not when requesting, so another
        // user's lease does not block this user from requesting access.
        var fixture = new AccessRuleEngineFixture()
            .RequiringSingleton()
            .WithActiveLeaseHeldBy(AccessRuleEngineFixture.AnotherUser);

        var result = fixture.RequestAccess(fixture.Cipher);

        Assert.Equal(RequestAccessOutcome.Created, result.Outcome);
    }

    // ExchangeRequestForLease: gate on approval, re-evaluate the rule against the stored signals,
    // enforce lease-issuance constraints, then issue the lease.

    [Fact]
    public void Exchange_NoRequest_FailsWithRequestNotFound()
    {
        var fixture = new AccessRuleEngineFixture();

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.RequestNotFound, result.FailReason);
    }

    [Fact]
    public void Exchange_NoRuleGovernsCipher_FailsWithNoRule()
    {
        var fixture = new AccessRuleEngineFixture()
            .WithNoRules();
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.NoRule, result.FailReason);
    }

    [Fact]
    public void Exchange_PermissiveRule_CreatesLease()
    {
        var fixture = new AccessRuleEngineFixture();
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
        Assert.NotNull(result.Lease);
        Assert.Equal(1, fixture.LeasesCreated);
    }

    [Fact]
    public void Exchange_ApprovalRequiredAndRequestNotApproved_FailsWithNotApproved()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval();
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.NotApproved, result.FailReason);
        Assert.Equal(0, fixture.LeasesCreated);
    }

    [Fact]
    public void Exchange_ApprovalRequiredAndRequestApproved_CreatesLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval();
        fixture.RequestAccess(fixture.Cipher);
        fixture.ApproveRequest();

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
        Assert.NotNull(result.Lease);
    }

    [Fact]
    public void Exchange_IpAddressOutsideRequiredCidr_FailsWithAccessDeniedNotWithinIpRange()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("192.168.1.5");
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.AccessDenied, result.FailReason);
        Assert.Equal(DenyReason.NotWithinIpRange, result.DenyReason);
    }

    [Fact]
    public void Exchange_IpAddressWithinRequiredCidr_CreatesLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("10.0.0.5");
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
    }

    [Fact]
    public void Exchange_UnparseableCidrEntryIsSkipped_AndALaterMatchCreatesLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("not-a-cidr", "10.0.0.0/24")
            .FromIpAddress("10.0.0.5");
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
    }

    [Fact]
    public void Exchange_OutsideTimeWindow_FailsWithAccessDeniedNotWithinTimeWindow()
    {
        // The fixture's signal time is 12:00 UTC, outside the 13:00-17:00 window.
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToTimeWindow("UTC", new TimeOnly(13, 0), new TimeOnly(17, 0));
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.AccessDenied, result.FailReason);
        Assert.Equal(DenyReason.NotWithinTimeWindow, result.DenyReason);
    }

    [Fact]
    public void Exchange_SingletonRequiredAndAnotherUserHoldsActiveLease_FailsWithSingletonHeld()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringSingleton()
            .WithActiveLeaseHeldBy(AccessRuleEngineFixture.AnotherUser);
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.SingletonHeld, result.FailReason);
        Assert.Equal(0, fixture.LeasesCreated);
    }

    [Fact]
    public void Exchange_SingletonRequiredAndNoExistingLease_CreatesLease()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringSingleton();
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
        Assert.Equal(1, fixture.LeasesCreated);
    }

    [Fact]
    public void Exchange_LeaseCreationFails_FailsWithLeaseCreationFailed()
    {
        var fixture = new AccessRuleEngineFixture()
            .WhereLeaseCreationFails();
        fixture.RequestAccess(fixture.Cipher);

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.LeaseCreationFailed, result.FailReason);
    }

    [Fact]
    public void Exchange_EvaluatesStoredSignals_GrantsEvenWhenLiveContextWouldBeDenied()
    {
        // Request from an allowed address, then move to a denied one before exchanging. The lease is
        // still issued because the rule is evaluated against the signals captured at request time.
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("10.0.0.5");
        fixture.RequestAccess(fixture.Cipher);
        fixture.FromIpAddress("192.168.1.5");

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, result.Outcome);
    }

    [Fact]
    public void Exchange_EvaluatesStoredSignals_DeniesEvenWhenLiveContextWouldBeAllowed()
    {
        // Request from a denied address, then move to an allowed one before exchanging. Access is
        // still denied because the rule is evaluated against the signals captured at request time.
        var fixture = new AccessRuleEngineFixture()
            .RestrictedToCidr("10.0.0.0/24")
            .FromIpAddress("192.168.1.5");
        fixture.RequestAccess(fixture.Cipher);
        fixture.FromIpAddress("10.0.0.5");

        var result = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Failed, result.Outcome);
        Assert.Equal(ExchangeFailReason.AccessDenied, result.FailReason);
        Assert.Equal(DenyReason.NotWithinIpRange, result.DenyReason);
    }

    [Fact]
    public void Exchange_DoesNotConsumeRequest_RequestRemainsAfterLeaseIssued()
    {
        var fixture = new AccessRuleEngineFixture();
        fixture.RequestAccess(fixture.Cipher);

        var first = fixture.Exchange(fixture.Cipher);
        var second = fixture.Exchange(fixture.Cipher);

        Assert.Equal(ExchangeOutcome.Created, first.Outcome);
        // The request is left in place, so a later exchange still finds it rather than reporting RequestNotFound.
        Assert.NotEqual(ExchangeFailReason.RequestNotFound, second.FailReason);
    }

    // The full request -> approve -> exchange -> check lifecycle.

    [Fact]
    public void Lifecycle_RequestApproveExchange_ThenCheckGrants()
    {
        var fixture = new AccessRuleEngineFixture()
            .RequiringApproval();

        // No lease yet, so a check is denied.
        Assert.Equal(DenyReason.NoLease, fixture.Check(fixture.Cipher).Reason);

        // The user requests access.
        Assert.Equal(RequestAccessOutcome.Created, fixture.RequestAccess(fixture.Cipher).Outcome);

        // The request cannot be exchanged until it is approved.
        Assert.Equal(ExchangeFailReason.NotApproved, fixture.Exchange(fixture.Cipher).FailReason);

        // The request is approved out of band.
        fixture.ApproveRequest();

        // The request can now be exchanged for a lease.
        var exchange = fixture.Exchange(fixture.Cipher);
        Assert.Equal(ExchangeOutcome.Created, exchange.Outcome);
        Assert.NotNull(exchange.Lease);

        // With a valid lease in hand, the check now grants access.
        Assert.Equal(AccessOutcome.Granted, fixture.Check(fixture.Cipher).Outcome);
    }
}
