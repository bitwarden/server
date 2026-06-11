using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
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
public class CancelAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CancelAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CancelAsync(userId, requestId));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_NotOwner_ThrowsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        // Someone else's request is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CancelAsync(userId, request.Id));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    [Theory]
    [BitAutoData(AccessRequestStatus.Approved)]
    [BitAutoData(AccessRequestStatus.Denied)]
    [BitAutoData(AccessRequestStatus.Cancelled)]
    [BitAutoData(AccessRequestStatus.ExpiredUnanswered)]
    public async Task CancelAsync_NotPending_ThrowsConflict(AccessRequestStatus status, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = status;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_Pending_CancelsAndNotifiesApprovers(AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id);

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CancelAsync(request.Id, _now);
        // The request just left the pending queue; approvers must re-fetch so it drops out of their inbox.
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
    }

    private static SutProvider<CancelAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<CancelAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
