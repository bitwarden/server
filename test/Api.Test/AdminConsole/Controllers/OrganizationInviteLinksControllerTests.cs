using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationInviteLinksController))]
[SutProviderCustomize]
public class OrganizationInviteLinksControllerTests
{
    [Theory, BitAutoData]
    public async Task Create_WithValidInput_Success(
        Guid orgId,
        OrganizationInviteLink inviteLink,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        inviteLink.OrganizationId = orgId;
        inviteLink.AllowedDomains = "[\"acme.com\"]";

        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "encrypted-key",
        };

        sutProvider.GetDependency<ICreateOrganizationInviteLinkCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var result = await sutProvider.Sut.Create(orgId, model);

        var createdResult = Assert.IsType<Created<OrganizationInviteLinkResponseModel>>(result);
        Assert.Equal($"organizations/{orgId}/invite-link", createdResult.Location);
        Assert.NotNull(createdResult.Value);
        Assert.Equal(inviteLink.Id, createdResult.Value.Id);
        Assert.Equal(inviteLink.Code, createdResult.Value.Code);
        Assert.Equal(orgId, createdResult.Value.OrganizationId);

        await sutProvider.GetDependency<ICreateOrganizationInviteLinkCommand>()
            .Received(1)
            .CreateAsync(Arg.Is<CreateOrganizationInviteLinkRequest>(r =>
                r.OrganizationId == orgId &&
                r.EncryptedInviteKey == "encrypted-key"));
    }

    [Theory, BitAutoData]
    public async Task Create_WithExistingLink_Returns409(
        Guid orgId,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = ["acme.com"],
            EncryptedInviteKey = "encrypted-key",
        };

        sutProvider.GetDependency<ICreateOrganizationInviteLinkCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkAlreadyExists()));

        var result = await sutProvider.Sut.Create(orgId, model);

        var jsonResult = Assert.IsType<JsonHttpResult<Bit.Core.Models.Api.ErrorResponseModel>>(result);
        Assert.Equal(StatusCodes.Status409Conflict, jsonResult.StatusCode);
    }

    [Theory, BitAutoData]
    public async Task Get_WhenLinkExists_ReturnsOkWithModel(
        Guid orgId,
        OrganizationInviteLink inviteLink,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        inviteLink.OrganizationId = orgId;
        inviteLink.AllowedDomains = "[\"acme.com\"]";

        sutProvider.GetDependency<IGetOrganizationInviteLinkQuery>()
            .GetAsync(orgId)
            .Returns(new CommandResult<OrganizationInviteLink>(inviteLink));

        var result = await sutProvider.Sut.Get(orgId);

        var okResult = Assert.IsType<Ok<OrganizationInviteLinkResponseModel>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Equal(inviteLink.Id, okResult.Value.Id);
        Assert.Equal(orgId, okResult.Value.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task Get_WhenNoLinkExists_ReturnsNotFound(
        Guid orgId,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        sutProvider.GetDependency<IGetOrganizationInviteLinkQuery>()
            .GetAsync(orgId)
            .Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkNotFound()));

        var result = await sutProvider.Sut.Get(orgId);

        var notFoundResult = Assert.IsType<NotFound<Bit.Core.Models.Api.ErrorResponseModel>>(result);
        Assert.NotNull(notFoundResult.Value);
    }

    [Theory, BitAutoData]
    public async Task Get_WhenInviteLinkNotAvailable_Returns400(
        Guid orgId,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        sutProvider.GetDependency<IGetOrganizationInviteLinkQuery>()
            .GetAsync(orgId)
            .Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkNotAvailable()));

        var result = await sutProvider.Sut.Get(orgId);

        var badRequestResult = Assert.IsType<BadRequest<Bit.Core.Models.Api.ErrorResponseModel>>(result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Theory, BitAutoData]
    public async Task Create_WithValidationError_Returns400(
        Guid orgId,
        SutProvider<OrganizationInviteLinksController> sutProvider)
    {
        var model = new CreateOrganizationInviteLinkRequestModel
        {
            AllowedDomains = [],
            EncryptedInviteKey = "encrypted-key",
        };

        sutProvider.GetDependency<ICreateOrganizationInviteLinkCommand>()
            .CreateAsync(Arg.Any<CreateOrganizationInviteLinkRequest>())
            .Returns(new CommandResult<OrganizationInviteLink>(new InviteLinkDomainsRequired()));

        var result = await sutProvider.Sut.Create(orgId, model);

        var badRequestResult = Assert.IsType<BadRequest<Bit.Core.Models.Api.ErrorResponseModel>>(result);
        Assert.NotNull(badRequestResult.Value);
    }
}
