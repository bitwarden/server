using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/invite-link")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.GenerateInviteLink)]
public class OrganizationInviteLinksController(
    ICreateOrganizationInviteLinkCommand createOrganizationInviteLinkCommand,
    IGetOrganizationInviteLinkQuery getOrganizationInviteLinkQuery,
    IUpdateOrganizationInviteLinkCommand updateOrganizationInviteLinkCommand,
    IDeleteOrganizationInviteLinkCommand deleteOrganizationInviteLinkCommand,
    IRefreshOrganizationInviteLinkCommand refreshOrganizationInviteLinkCommand)
    : BaseAdminConsoleController
{
    [HttpGet("")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Get(Guid orgId)
    {
        var result = await getOrganizationInviteLinkQuery.GetAsync(orgId);

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpPost("")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Create(Guid orgId, [FromBody] CreateOrganizationInviteLinkRequestModel model)
    {
        var result = await createOrganizationInviteLinkCommand.CreateAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Created(
                $"organizations/{orgId}/invite-link",
                new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpPut("")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Update(Guid orgId, [FromBody] UpdateOrganizationInviteLinkRequestModel model)
    {
        var result = await updateOrganizationInviteLinkCommand.UpdateAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpDelete("")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Delete(Guid orgId)
    {
        var result = await deleteOrganizationInviteLinkCommand.DeleteAsync(orgId);
        return Handle(result);
    }

    [HttpPost("refresh")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Refresh(Guid orgId, [FromBody] RefreshOrganizationInviteLinkRequestModel model)
    {
        var result = await refreshOrganizationInviteLinkCommand.RefreshAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }
}
