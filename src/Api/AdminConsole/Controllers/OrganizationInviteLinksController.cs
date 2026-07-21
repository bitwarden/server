using Bit.Api.AdminConsole.Authorization;
using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Api.Models.Response;
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
    IGetOrganizationInviteLinkStatusQuery getOrganizationInviteLinkStatusQuery,
    IUpdateOrganizationInviteLinkCommand updateOrganizationInviteLinkCommand,
    IUpdateInviteSupportConfirmCommand updateInviteSupportConfirmCommand,
    IDeleteOrganizationInviteLinkCommand deleteOrganizationInviteLinkCommand,
    IRefreshOrganizationInviteLinkCommand refreshOrganizationInviteLinkCommand,
    IValidateOrganizationInviteLinkEmailDomainQuery validateOrganizationInviteLinkEmailDomainQuery,
    IGetOrganizationInviteLinkPoliciesQuery getOrganizationInviteLinkPoliciesQuery)
    : BaseAdminConsoleController
{
    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/status")]
    public async Task<IResult> GetStatus([FromBody] GetOrganizationInviteLinkStatusRequestModel model)
    {
        var result = await getOrganizationInviteLinkStatusQuery.GetStatusAsync(model.Code);

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
    public async Task<IResult> GetPolicies([FromBody] GetOrganizationInviteLinkPoliciesRequestModel model)
    {
        var result = await getOrganizationInviteLinkPoliciesQuery.GetPoliciesAsync(model.Code);
        return Handle(result, policies =>
            TypedResults.Ok(new ListResponseModel<PolicyResponseModel>(
                policies.Select(p => new PolicyResponseModel(p)))));
    }

    [AllowAnonymous]
    [HttpPost("/organizations/invite-link/validate-email-domain")]
    public async Task<IResult> ValidateEmailDomain(
        [FromBody] OrganizationInviteLinkValidateEmailDomainRequestModel model)
    {
        var result = await validateOrganizationInviteLinkEmailDomainQuery.ValidateAsync(model.Code, model.Email);

        return Handle(result, isAllowed =>
            TypedResults.Ok(new OrganizationInviteLinkValidateEmailDomainResponseModel(isAllowed)));
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

    [HttpPut("support-confirm")]
    [Authorize<ManageUsersRequirement>]
    public async Task<IResult> UpdateInviteSupportConfirm(Guid orgId, [FromBody] UpdateInviteSupportConfirmRequestModel model)
    {
        var result = await updateInviteSupportConfirmCommand.UpdateAsync(
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
