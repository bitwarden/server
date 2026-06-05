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
public class DecideLeaseRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task DecideAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ILeaseRequestRepository>().GetByIdAsync(requestId).Returns((LeaseRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DecideAsync(userId, requestId, Approve()));
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_NotManageable_ThrowsNotFound(Guid userId, LeaseRequest request)
    {
        var sutProvider = Setup();
        request.Status = LeaseRequestStatus.Pending;
        sutProvider.GetDependency<ILeaseRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, request.CollectionId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_NotPending_ThrowsConflict(Guid userId, LeaseRequest request)
    {
        var sutProvider = Setup();
        request.Status = LeaseRequestStatus.Approved;
        SetupManageableRequest(sutProvider, userId, request);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_SelfApproval_ThrowsBadRequest(Guid userId, LeaseRequest request)
    {
        var sutProvider = Setup();
        request.Status = LeaseRequestStatus.Pending;
        request.RequesterId = userId;
        SetupManageableRequest(sutProvider, userId, request);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DecideAsync(userId, request.Id, Approve()));
        Assert.Contains("your own request", ex.Message);
        await sutProvider.GetDependency<ILeaseRequestRepository>().DidNotReceiveWithAnyArgs()
            .ResolveWithDecisionAsync(default!, default!, default, default, default);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Approve_ResolvesAndWritesHumanDecision(Guid userId, LeaseRequest request)
    {
        var sutProvider = Setup();
        request.Status = LeaseRequestStatus.Pending;
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Approve("looks good"));

        Assert.Equal(LeaseRequestStatus.Approved, result.Status);
        Assert.Equal(_now, result.ResolvedDate);
        Assert.Equal(userId, result.ResolverId);
        Assert.Equal("looks good", result.ResolverComment);
        await sutProvider.GetDependency<ILeaseRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<LeaseDecision>(d =>
                d.DeciderKind == LeaseDecisionKind.Human &&
                d.ApproverId == userId &&
                d.Decision == LeaseDecisionVerdict.Approve &&
                d.Comment == "looks good"),
            LeaseRequestStatus.Approved,
            // Approval mints an active lease spanning the request's approved window.
            Arg.Is<Lease>(l =>
                l.LeaseRequestId == request.Id &&
                l.OrganizationId == request.OrganizationId &&
                l.CollectionId == request.CollectionId &&
                l.CipherId == request.CipherId &&
                l.RequesterId == request.RequesterId &&
                l.Status == LeaseStatus.Active &&
                l.NotBefore == request.NotBefore &&
                l.NotAfter == request.NotAfter &&
                l.Id != default),
            _now);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task DecideAsync_Deny_ResolvesAsDenied(Guid userId, LeaseRequest request)
    {
        var sutProvider = Setup();
        request.Status = LeaseRequestStatus.Pending;
        SetupManageableRequest(sutProvider, userId, request);

        var result = await sutProvider.Sut.DecideAsync(userId, request.Id, Deny());

        Assert.Equal(LeaseRequestStatus.Denied, result.Status);
        await sutProvider.GetDependency<ILeaseRequestRepository>().Received(1).ResolveWithDecisionAsync(
            request,
            Arg.Is<LeaseDecision>(d => d.Decision == LeaseDecisionVerdict.Deny),
            LeaseRequestStatus.Denied,
            // A denial creates no lease.
            null,
            _now);
    }

    private static LeaseDecisionSubmission Approve(string? comment = null) =>
        new() { Verdict = LeaseDecisionVerdict.Approve, Comment = comment };

    private static LeaseDecisionSubmission Deny(string? comment = null) =>
        new() { Verdict = LeaseDecisionVerdict.Deny, Comment = comment };

    private static SutProvider<DecideLeaseRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<DecideLeaseRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupManageableRequest(SutProvider<DecideLeaseRequestCommand> sutProvider, Guid userId, LeaseRequest request)
    {
        sutProvider.GetDependency<ILeaseRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(userId, request.CollectionId).Returns(true);
    }
}
