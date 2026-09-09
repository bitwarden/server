using System.Net;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
using Bit.Core;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.Utilities;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("organizations/{orgId}/invite-link")]
[Authorize("Application")]
[RequireFeature(FeatureFlagKeys.GenerateInviteLink)]
public class OrganizationInviteLinksController(
    ICreateOrganizationInviteLinkCommand createOrganizationInviteLinkCommand,
    IGetOrganizationInviteLinkQuery getOrganizationInviteLinkQuery,
    IGetOrganizationInviteLinkStatusQuery getOrganizationInviteLinkStatusQuery,
    IUpdateOrganizationInviteLinkCommand updateOrganizationInviteLinkCommand,
    IUpdateInviteSupportConfirmCommand updateInviteSupportConfirmCommand,
    IDeleteOrganizationInviteLinkCommand deleteOrganizationInviteLinkCommand,
    IRefreshOrganizationInviteLinkCommand refreshOrganizationInviteLinkCommand,
    IValidateOrganizationInviteLinkQuery validateOrganizationInviteLinkQuery,
    IGetOrganizationInviteLinkPoliciesQuery getOrganizationInviteLinkPoliciesQuery)
    : BaseAdminConsoleController
{
    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/status")]
    [ProducesResponseType(typeof(OrganizationInviteLinkStatusResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> GetStatus([FromBody] GetOrganizationInviteLinkStatusRequestModel model)
    {
        var result = await getOrganizationInviteLinkStatusQuery.GetStatusAsync(model.OrganizationId, model.Code);

        return Handle(result, status =>
            TypedResults.Ok(new OrganizationInviteLinkStatusResponseModel(
                status.OrganizationName,
                status.LinksEnabled,
                status.SeatsAvailable,
                status.SupportsConfirmation,
                status.Sso is null
                    ? null
                    : new OrganizationInviteLinkSsoResponseModel(status.Sso.OrgSsoId, status.Sso.Required))));
    }

    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/policies")]
    [ProducesResponseType(typeof(ListResponseModel<PolicyResponseModel>), (int)HttpStatusCode.OK)]
    public async Task<IResult> GetPolicies([FromBody] GetOrganizationInviteLinkPoliciesRequestModel model)
    {
        var result = await getOrganizationInviteLinkPoliciesQuery.GetPoliciesAsync(model.OrganizationId, model.Code);
        return Handle(result, policies =>
            TypedResults.Ok(new ListResponseModel<PolicyResponseModel>(
                policies.Select(p => new PolicyResponseModel(p)))));
    }

    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/validate-email-domain")]
    [ProducesResponseType(typeof(OrganizationInviteLinkValidateEmailDomainResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> ValidateEmailDomain(
        [FromBody] OrganizationInviteLinkValidateEmailDomainRequestModel model)
    {
        var result = await validateOrganizationInviteLinkQuery.ValidateAsync(model.OrganizationId, model.Code, model.Email);

        // Preserve the existing client contract: report the domain check as an IsAllowed boolean
        // rather than surfacing a disallowed domain as an error status.
        if (result is { IsError: true, AsError: EmailDomainNotAllowed })
        {
            return TypedResults.Ok(new OrganizationInviteLinkValidateEmailDomainResponseModel(false));
        }

        return Handle(result, _ =>
            TypedResults.Ok(new OrganizationInviteLinkValidateEmailDomainResponseModel(true)));
    }

    [HttpGet("")]
    [Authorize<ManageUsersRequirement>]
    [ProducesResponseType(typeof(OrganizationInviteLinkResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> Get([FromRoute] Guid orgId)
    {
        var result = await getOrganizationInviteLinkQuery.GetAsync(orgId);

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpPost("")]
    [Authorize<ManageUsersRequirement>]
    [ProducesResponseType(typeof(OrganizationInviteLinkResponseModel), (int)HttpStatusCode.Created)]
    public async Task<IResult> Create([FromRoute] Guid orgId, [FromBody] CreateOrganizationInviteLinkRequestModel model)
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
    [ProducesResponseType(typeof(OrganizationInviteLinkResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> Update([FromRoute] Guid orgId, [FromBody] UpdateOrganizationInviteLinkRequestModel model)
    {
        var result = await updateOrganizationInviteLinkCommand.UpdateAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpPut("support-confirm")]
    [Authorize<ManageUsersRequirement>]
    [ProducesResponseType(typeof(OrganizationInviteLinkResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> UpdateInviteSupportConfirm([FromRoute] Guid orgId, [FromBody] UpdateInviteSupportConfirmRequestModel model)
    {
        var result = await updateInviteSupportConfirmCommand.UpdateAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }

    [HttpDelete("")]
    [Authorize<ManageUsersRequirement>]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    public async Task<IResult> Delete([FromRoute] Guid orgId)
    {
        var result = await deleteOrganizationInviteLinkCommand.DeleteAsync(orgId);
        return Handle(result);
    }

    [HttpPost("refresh")]
    [Authorize<ManageUsersRequirement>]
    [ProducesResponseType(typeof(OrganizationInviteLinkResponseModel), (int)HttpStatusCode.OK)]
    public async Task<IResult> Refresh([FromRoute] Guid orgId, [FromBody] RefreshOrganizationInviteLinkRequestModel model)
    {
        var result = await refreshOrganizationInviteLinkCommand.RefreshAsync(
            model.ToCommandRequest(orgId));

        return Handle(result, link =>
            TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));
    }
}
