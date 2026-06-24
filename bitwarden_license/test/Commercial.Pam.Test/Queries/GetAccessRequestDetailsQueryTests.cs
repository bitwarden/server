using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Commercial.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class GetAccessRequestDetailsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetDetailsAsync_RequestMissing_ThrowsNotFound(
        Guid userId, Guid requestId, SutProvider<GetAccessRequestDetailsQuery> sutProvider)
    {
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(requestId)
            .Returns((AccessRequestDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetailsAsync(userId, requestId));
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_Requester_ReturnsDetailsWithoutManagerCheck(
        Guid userId, AccessRequestDetails details, SutProvider<GetAccessRequestDetailsQuery> sutProvider)
    {
        details.RequesterId = userId;
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id).Returns(details);

        var result = await sutProvider.Sut.GetDetailsAsync(userId, details.Id);

        Assert.Same(details, result);
        // The requester always sees their own request — no collection-manage check, and (unlike decide) no
        // self-approval block.
        await sutProvider.GetDependency<IApproverCollectionAccessQuery>().DidNotReceiveWithAnyArgs()
            .CanManageCollectionAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_ManagingApprover_ReturnsDetails(
        Guid managerId, AccessRequestDetails details, SutProvider<GetAccessRequestDetailsQuery> sutProvider)
    {
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(managerId, details.CollectionId).Returns(true);

        var result = await sutProvider.Sut.GetDetailsAsync(managerId, details.Id);

        Assert.Same(details, result);
    }

    [Theory, BitAutoData]
    public async Task GetDetailsAsync_NeitherRequesterNorManager_ThrowsNotFound(
        Guid userId, AccessRequestDetails details, SutProvider<GetAccessRequestDetailsQuery> sutProvider)
    {
        sutProvider.GetDependency<IAccessRequestRepository>().GetDetailsByIdAsync(details.Id).Returns(details);
        // userId is neither the requester nor a manager (CanManageCollectionAsync defaults to false).

        // A request the caller can't see is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.GetDetailsAsync(userId, details.Id));
    }
}
