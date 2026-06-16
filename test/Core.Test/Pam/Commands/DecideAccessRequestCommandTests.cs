using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

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
        request.Status = AccessRequestStatus.Pending;
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
    public async Task DecideAsync_NotPending_ThrowsConflict(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Approved;
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
        request.Status = AccessRequestStatus.Pending;
        request.RequesterId = userId;
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        Assert.Contains("your own request", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_WindowAlreadyEnded_ThrowsBadRequest(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
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
    public async Task DecideAsync_Deny_WindowAlreadyEnded_Succeeds(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);
        SetupManageableRequest(sutProvider, userId, request);

        // A lapsed window only blocks approval (it could never be activated); denial still closes the request out.
        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Deny());

        Assert.Equal(AccessRequestStatus.Denied, result.Status);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_ResolvesAndWritesHumanDecision(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Approve("looks good"));

        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Equal(_now, result.ResolvedDate);
        var decision = Assert.Single(result.Decisions);
        Assert.Equal(AccessDeciderKind.Human, decision.DeciderKind);
        Assert.Equal(userId, decision.Id!.Value);
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
            AccessRequestStatus.Approved,
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Deny_ResolvesAsDenied(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        SetOpenWindow(request);
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Deny());

        Assert.Equal(AccessRequestStatus.Denied, result.Status);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d => d.Verdict == AccessDecisionVerdict.Deny),
            AccessRequestStatus.Denied,
            _now);
        // A denial reaches the requester too (their "My requests" view flips to denied).
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
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
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, request.CollectionId).Returns(true);
    }

    // BitAutoData generates arbitrary dates; pin a window containing _now so the lapsed-window approve guard
    // doesn't trip in tests that aren't about it.
    private static void SetOpenWindow(AccessRequest request)
    {
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
    }
}
