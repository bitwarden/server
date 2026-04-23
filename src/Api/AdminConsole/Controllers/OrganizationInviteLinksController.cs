using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/invite-link")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.GenerateInviteLink)]
public class OrganizationInviteLinksController(
    ICreateOrganizationInviteLinkCommand createOrganizationInviteLinkCommand,
    IOrganizationInviteLinkRepository organizationInviteLinkRepository)
    : BaseAdminConsoleController
{
    [HttpGet("")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> Get(Guid orgId)
    {
        var link = await organizationInviteLinkRepository.GetByOrganizationIdAsync(orgId);

        return link is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new OrganizationInviteLinkResponseModel(link));
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
}
