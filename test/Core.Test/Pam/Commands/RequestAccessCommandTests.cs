using Bit.Core.Exceptions;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Rules;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

[SutProviderCustomize]
public class RequestAccessCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_CipherNotAccessible_ThrowsNotFound(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_NotLeasingGated_ThrowsBadRequest(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IAccessApprovalResolver>().ResolveAsync(userId, cipherId)
            .Returns((AccessApprovalResolution?)null);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("does not require a lease", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_Automatic_IssuesActiveLease(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupPolicyDecision(sutProvider, AccessDecision.Allow);

        var result = await sutProvider.Sut.RequestAccessAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" });

        Assert.Equal(AccessApprovalOutcome.Automatic, result.Outcome);
        Assert.NotNull(result.Lease);
        Assert.Equal(LeaseStatus.Active, result.Lease!.Status);
        Assert.Equal(_now, result.Lease.NotBefore);
        Assert.Equal(_now.AddSeconds(3600), result.Lease.NotAfter);
        await sutProvider.GetDependency<ILeaseRepository>().Received(1)
            .CreateAutoApprovedAsync(Arg.Any<LeaseRequest>(), Arg.Any<LeaseDecision>(), Arg.Any<Lease>(), _now);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_AutomaticWithWindow_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now, End = _now.AddHours(1) }));
        Assert.Contains("provide a duration", ex.Message);
        await sutProvider.GetDependency<ILeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_AutomaticMissingDuration_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId, new AccessRequestSubmission()));
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_AutomaticDurationExceedsMax_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = RequestAccessCommand.MaxDurationSeconds + 1 }));
        Assert.Contains("maximum", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_AutomaticPolicyDenied_ThrowsBadRequestAndIssuesNoLease(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupPolicyDecision(sutProvider, AccessDecision.Deny(DenyReason.NotWithinIpRange));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("network", ex.Message);
        // A rule the caller fails to satisfy must not produce a lease.
        await sutProvider.GetDependency<ILeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_Human_CreatesPendingRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<ILeaseRequestRepository>()
            .CreateAsync(Arg.Any<LeaseRequest>())
            .Returns(callInfo => callInfo.Arg<LeaseRequest>());

        var start = _now.AddHours(1);
        var end = _now.AddHours(2);
        var result = await sutProvider.Sut.RequestAccessAsync(userId, cipherId,
            new AccessRequestSubmission { Start = start, End = end, Reason = "audit" });

        Assert.Equal(AccessApprovalOutcome.Human, result.Outcome);
        Assert.NotNull(result.Request);
        Assert.Equal(LeaseRequestStatus.Pending, result.Request!.Status);
        Assert.Equal(start, result.Request.NotBefore);
        Assert.Equal(end, result.Request.NotAfter);
        Assert.Equal("audit", result.Request.Reason);
        await sutProvider.GetDependency<ILeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!, default!, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(collectionId);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_Automatic_DoesNotNotifyApprovers(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        await sutProvider.Sut.RequestAccessAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" });

        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_HumanMissingReason_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2) }));
        Assert.Contains("reason is required", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_HumanWithDuration_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = 3600, Reason = "x" }));
        Assert.Contains("requires human approval", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_HumanStartNotBeforeEnd_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(2), End = _now.AddHours(1), Reason = "x" }));
        Assert.Contains("before the end date", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_ExistingActiveLease_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, Lease lease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        sutProvider.GetDependency<ILeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("already have active access", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task RequestAccessAsync_ExistingPendingRequest_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, LeaseRequest pending)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<ILeaseRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId)
            .Returns(pending);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.RequestAccessAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2), Reason = "x" }));
        Assert.Contains("already have a pending request", ex.Message);
    }

    private static SutProvider<RequestAccessCommand> Setup()
    {
        var sutProvider = new SutProvider<RequestAccessCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupCipher(SutProvider<RequestAccessCommand> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }

    private static void SetupResolution(SutProvider<RequestAccessCommand> sutProvider, Guid userId, Guid cipherId,
        Guid orgId, Guid collectionId, bool requiresHuman)
    {
        var rule = requiresHuman ? new HumanApprovalRule() : (Rule)new IpAllowlistRule { Cidrs = ["10.0.0.0/8"] };
        sutProvider.GetDependency<IAccessApprovalResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns(new AccessApprovalResolution(orgId, collectionId, requiresHuman, rule));
    }

    private static void SetupPolicyDecision(SutProvider<RequestAccessCommand> sutProvider, AccessDecision decision)
    {
        sutProvider.GetDependency<IAccessPolicyEngine>()
            .Evaluate(Arg.Any<Rule>(), Arg.Any<AccessPolicySignals>())
            .Returns(decision);
    }
}
