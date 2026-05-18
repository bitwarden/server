using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationInviteLinksPublicController))]
[SutProviderCustomize]
public class OrganizationInviteLinksPublicControllerTests
{
    [Theory, BitAutoData]
    public async Task GetStatus_WithValidQuery_Success(
        Guid code,
        OrganizationInviteLinkStatus status,
        SutProvider<OrganizationInviteLinksPublicController> sutProvider)
    {
        sutProvider.GetDependency<IGetOrganizationInviteLinkStatusQuery>()
            .GetStatusAsync(code)
            .Returns(new CommandResult<OrganizationInviteLinkStatus>(status));

        var result = await sutProvider.Sut.GetStatus(code);

        var okResult = Assert.IsType<Ok<OrganizationInviteLinkStatusResponseModel>>(result);
        Assert.Equal(status.OrganizationId, okResult.Value!.OrganizationId);
        Assert.Equal(status.OrganizationName, okResult.Value.OrganizationName);
        Assert.Equal(status.SeatsAvailable, okResult.Value.SeatsAvailable);
    }

    [Theory, BitAutoData]
    public async Task GetStatus_WithNotFoundError_ReturnsNotFound(
        Guid code,
        SutProvider<OrganizationInviteLinksPublicController> sutProvider)
    {
        sutProvider.GetDependency<IGetOrganizationInviteLinkStatusQuery>()
            .GetStatusAsync(code)
            .Returns(new CommandResult<OrganizationInviteLinkStatus>(new InviteLinkNotFound()));

        var result = await sutProvider.Sut.GetStatus(code);

        Assert.IsType<NotFound<Bit.Core.Models.Api.ErrorResponseModel>>(result);
    }
}
