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
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_NotPending_ThrowsConflict(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Approved;
        SetupManageableRequest(sutProvider, userId, request);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
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
            .ResolveWithDecisionAsync(default!, default!, default, default, default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_ResolvesAndWritesHumanDecision(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Approve("looks good"));

        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Equal(_now, result.ResolvedDate);
        Assert.Equal(userId, result.ApproverId);
        Assert.Equal("looks good", result.ApproverComment);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d =>
                d.DeciderKind == AccessDeciderKind.Human &&
                d.ApproverId == userId &&
                d.Verdict == AccessDecisionVerdict.Approve &&
                d.Comment == "looks good"),
            AccessRequestStatus.Approved,
            // Approval mints an active lease spanning the request's approved window.
            Arg.Is<AccessLease>(l =>
                l.AccessRequestId == request.Id &&
                l.OrganizationId == request.OrganizationId &&
                l.CollectionId == request.CollectionId &&
                l.CipherId == request.CipherId &&
                l.RequesterId == request.RequesterId &&
                l.Status == AccessLeaseStatus.Active &&
                l.NotBefore == request.NotBefore &&
                l.NotAfter == request.NotAfter &&
                l.Id != default),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Deny_ResolvesAsDenied(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Deny());

        Assert.Equal(AccessRequestStatus.Denied, result.Status);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d => d.Verdict == AccessDecisionVerdict.Deny),
            AccessRequestStatus.Denied,
            // A denial creates no lease.
            null,
            _now);
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
}
