using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request;
using Bit.Core.Context;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationAuthRequestsController))]
[SutProviderCustomize]
public class OrganizationAuthRequestsControllerTests
{

    [Theory]
    [BitAutoData]
    public async Task ValidateAdminRequest_UserDoesNotHaveManageResetPasswordPermissions_ThrowsUnauthorized(
        SutProvider<OrganizationAuthRequestsController> sutProvider,
        Guid organizationId
    )
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.ValidateAdminRequest(organizationId));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAdminRequest_UserHasManageResetPasswordPermissions_DoesNotThrow(
        SutProvider<OrganizationAuthRequestsController> sutProvider,
        Guid organizationId
    )
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(true);
        await sutProvider.Sut.ValidateAdminRequest(organizationId);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateManyAuthRequests_ValidInput_DoesNotThrow(
        SutProvider<OrganizationAuthRequestsController> sutProvider,
        IEnumerable<OrganizationAuthRequestUpdateManyRequestModel> request,
        Guid organizationId
    )
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(true);
        await sutProvider.Sut.UpdateManyAuthRequests(organizationId, request);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateManyAuthRequests_NotPermissioned_ThrowsUnauthorized(
        SutProvider<OrganizationAuthRequestsController> sutProvider,
        IEnumerable<OrganizationAuthRequestUpdateManyRequestModel> request,
        Guid organizationId
    )
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sutProvider.Sut.UpdateManyAuthRequests(organizationId, request));
    }
}
