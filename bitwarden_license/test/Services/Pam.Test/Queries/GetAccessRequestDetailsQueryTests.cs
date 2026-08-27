using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class GetAccessRequestDetailsQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(requestId, _now)
            .Returns((AccessRequestDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetailsAsync(userId, requestId, _now));
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_Requester_ReturnsDetailsWithoutManagerCheck(
        Guid userId, AccessRequestDetails details)
    {
        var sutProvider = Setup();
        details.RequesterId = userId;
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id, _now).Returns(details);

        var result = await sutProvider.Sut.GetDetailsAsync(userId, details.Id, _now);

        Assert.Same(details, result);
        // The requester always sees their own request — no collection-manage check, and (unlike decide) no
        // self-approval block.
        await sutProvider.GetDependency<IApproverCollectionAccessQuery>().DidNotReceiveWithAnyArgs()
            .CanManageCollectionAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_ManagingApprover_ReturnsDetails(Guid managerId, AccessRequestDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id, _now).Returns(details);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(managerId, details.CollectionId).Returns(true);

        var result = await sutProvider.Sut.GetDetailsAsync(managerId, details.Id, _now);

        Assert.Same(details, result);
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_NeitherRequesterNorManager_ThrowsNotFound(
        Guid userId, AccessRequestDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id, _now).Returns(details);
        // userId is neither the requester nor a manager (CanManageCollectionAsync defaults to false).

        // A request the caller can't see is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetailsAsync(userId, details.Id, _now));
    }

    /// <summary>
    /// The read is handed the current time so the produced lease's status can be projected against it: nothing
    /// writes AccessLeaseStatus.Expired, so a lapsed lease reads as Active unless a clock reinterprets it
    /// (PM-42355).
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetDetailsAsync_PassesCurrentTimeAsTheProjectionClock(AccessRequestDetails details)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetDetailsByIdAsync(details.Id, Arg.Any<DateTime>()).Returns(details);

        await sutProvider.Sut.GetDetailsAsync(details.RequesterId, details.Id, _now);

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .GetDetailsByIdAsync(details.Id, _now);
    }

    private static SutProvider<GetAccessRequestDetailsQuery> Setup()
    {
        var sutProvider = new SutProvider<GetAccessRequestDetailsQuery>().Create();
        return sutProvider;
    }
}
