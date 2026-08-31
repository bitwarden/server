using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class DecideAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DecideAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DecideAsync(userId, requestId, Approve()));
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_NotManageable_ThrowsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, request.CollectionId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_ExtensionRequest_ThrowsBadRequestWithoutResolving(
        Guid userId, AccessRequest request, Guid parentLeaseId)
    {
        var sutProvider = Setup();
        // An open extension is unreachable today, so this pins the guard against a future human-approved
        // extension path rather than a shape the server can currently produce: were one routed here, resolving it
        // would leave an activatable approval and reopen the second-lease hole.
        request.Action = AccessRequestAction.None;
        SetupManageableRequest(sutProvider, userId, request);
        request.ExtensionOfLeaseId = parentLeaseId;
        SetOpenWindow(request);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_NotPending_ThrowsConflict(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.Approved;
        SetupManageableRequest(sutProvider, userId, request);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_SelfApproval_ThrowsBadRequest(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        SetOpenWindow(request);
        request.RequesterId = userId;
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        Assert.Contains("your own request", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
        await sutProvider.GetDependency<IRequesterMailNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyDecisionAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_WindowAlreadyEnded_ThrowsConflict(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        Assert.Contains("already ended", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Deny_WindowAlreadyEnded_ThrowsConflict(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);
        SetupManageableRequest(sutProvider, userId, request);

        // The clock closed the request: it reads as Expired everywhere, and neither verdict may restamp it -- a
        // denial would rewrite a row users already saw as Expired. (This retires the old "denial still closes the
        // audit trail out" behavior.) Denied without a reason on purpose: a lapsed request is refused for having
        // lapsed, not sent back to be resubmitted with a reason that would change nothing.
        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Deny()));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_ResolvesAndWritesHumanDecision(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Approve("looks good"));

        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Equal(_now, result.ResolvedDate);
        var decision = Assert.Single(result.Decisions);
        Assert.Equal(AccessDeciderKind.Human, decision.DeciderKind);
        Assert.Equal(userId, decision.ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Approve, decision.Verdict);
        Assert.Equal("looks good", decision.Comment);
        Assert.Equal(_now, decision.DecidedAt);
        // Approval records the verdict only; no lease is minted until the requester activates the approved request.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d =>
                d.DeciderKind == AccessDeciderKind.Human &&
                d.ApproverId == userId &&
                d.Verdict == AccessDecisionVerdict.Approve &&
                d.Comment == "looks good"),
            AccessRequestAction.Approved,
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
        await sutProvider.GetDependency<IRequesterMailNotifier>().Received(1)
            .NotifyDecisionAsync(request, true);
    }

    [Theory]
    [BitAutoData((string?)null)]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task DecideAsync_Deny_WithoutReason_ThrowsBadRequest(
        string? comment, Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Deny(comment)));
        Assert.Contains("reason is required", ex.Message);
        // Nothing is written and nobody is told: a denial the requester cannot be given a reason for must leave the
        // request pending so the approver can resubmit it with one.
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().DidNotReceiveWithAnyArgs()
            .EmitAsync(default!);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_WithoutComment_Resolves(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        // The required-reason gate is the denial's alone; an approval still needs no explanation.
        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Approve());

        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Null(Assert.Single(result.Decisions).Comment);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Deny_ResolvesAsDenied(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Deny("use the read replica instead"));

        Assert.Equal(AccessRequestStatus.Denied, result.Status);
        Assert.Equal("use the read replica instead", Assert.Single(result.Decisions).Comment);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d =>
                d.Verdict == AccessDecisionVerdict.Deny &&
                d.Comment == "use the read replica instead"),
            AccessRequestAction.Denied,
            _now);
        // A denial reaches the requester too (their "My requests" view flips to denied).
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
        await sutProvider.GetDependency<IRequesterMailNotifier>().Received(1)
            .NotifyDecisionAsync(request, false);
    }

    private static AccessDecisionSubmission Approve(string? comment = null) =>
        new() { Verdict = AccessDecisionVerdict.Approve, Comment = comment };

    private static AccessDecisionSubmission Deny(string? comment = null) =>
        new() { Verdict = AccessDecisionVerdict.Deny, Comment = comment };

    private static SutProvider<DecideAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<DecideAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupManageableRequest(SutProvider<DecideAccessRequestCommand> sutProvider, Guid userId, AccessRequest request)
    {
        // BitAutoData fills every nullable, ExtensionOfLeaseId included. An extension is never decided, so a fixture
        // left as generated models a request no approver can act on -- pin it null so these tests exercise an ordinary
        // request, and set it explicitly in the test that is about extensions.
        request.ExtensionOfLeaseId = null;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, request.CollectionId).Returns(true);
    }

    // BitAutoData generates arbitrary dates; pin a window containing _now so the lapsed-window guard
    // doesn't trip in tests that aren't about it.
    private static void SetOpenWindow(AccessRequest request)
    {
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
    }
}
