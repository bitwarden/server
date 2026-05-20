using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities;
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
    IRefreshOrganizationInviteLinkCommand refreshOrganizationInviteLinkCommand,
    IOrganizationInviteLinkRepository organizationInviteLinkRepository)
    : BaseAdminConsoleController
{
    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/validate-email-domain")]
    public async Task<IResult> ValidateEmailDomain(
        [FromBody] OrganizationInviteLinkValidateEmailDomainRequestModel model)
    {
        var link = await organizationInviteLinkRepository.GetByCodeAsync(model.Code);
        if (link is null)
        {
            return TypedResults.NotFound();
        }

        var isAllowed = InviteLinkDomainValidator.IsEmailDomainAllowed(model.Email, link.GetAllowedDomains());
        var response = new OrganizationInviteLinkValidateEmailDomainResponseModel(isAllowed);
        return TypedResults.Ok(response);
    }

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
