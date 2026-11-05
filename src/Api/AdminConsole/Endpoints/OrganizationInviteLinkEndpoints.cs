using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.AdminConsole.Models.Response.Organizations;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.HttpExtensions;
using Bit.OrganizationAuthorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Endpoints;

public static class OrganizationInviteLinkEndpoints
{
    private const string _tag = "OrganizationInviteLinks";

    extension(IEndpointRouteBuilder endpoints)
    {
        public RouteGroupBuilder MapOrganizationInviteLinkEndpoints()
        {
            var group = endpoints.MapGroup("")
                .WithAdminConsoleDefaults(_tag)
                .RequireAuthorization(new AuthorizeAttribute<ManageUsersRequirement>());

            group.MapGet("", GetAsync)
                .WithName($"{_tag}_Get")
                .Produces<OrganizationInviteLinkResponseModel>();

            group.MapPost("", CreateAsync)
                .WithName($"{_tag}_Create")
                .Produces<OrganizationInviteLinkResponseModel>(StatusCodes.Status201Created);

            group.MapPut("", UpdateAsync)
                .WithName($"{_tag}_Update")
                .Produces<OrganizationInviteLinkResponseModel>();

            group.MapPut("support-confirm", UpdateInviteSupportConfirmAsync)
                .WithName($"{_tag}_UpdateInviteSupportConfirm")
                .Produces<OrganizationInviteLinkResponseModel>();

            group.MapDelete("", DeleteAsync)
                .WithName($"{_tag}_Delete")
                .Produces(StatusCodes.Status204NoContent);

            group.MapPost("refresh", RefreshAsync)
                .WithName($"{_tag}_Refresh")
                .Produces<OrganizationInviteLinkResponseModel>();

            return group;
        }

        public RouteGroupBuilder MapAnonymousOrganizationInviteLinkEndpoints()
        {
            var group = endpoints.MapGroup("")
                .WithAdminConsoleDefaults(_tag)
                .AllowAnonymous();

            group.MapPost("status", GetStatusAsync)
                .WithName($"{_tag}_GetStatus")
                .Produces<OrganizationInviteLinkStatusResponseModel>();

            group.MapPost("policies", GetPoliciesAsync)
                .WithName($"{_tag}_GetPolicies")
                .Produces<ListResponseModel<PolicyResponseModel>>();

            group.MapPost("validate-email-domain", ValidateEmailDomainAsync)
                .WithName($"{_tag}_ValidateEmailDomain")
                .Produces<OrganizationInviteLinkValidateEmailDomainResponseModel>();

            return group;
        }
    }

    public static async Task<IResult> GetAsync(
        [FromRoute] Guid orgId,
        IGetOrganizationInviteLinkQuery query) =>
        (await query.GetAsync(orgId))
            .ToResult(link => TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));

    public static async Task<IResult> CreateAsync(
        [FromRoute] Guid orgId,
        [FromBody] CreateOrganizationInviteLinkRequestModel model,
        ICreateOrganizationInviteLinkCommand command) =>
        (await command.CreateAsync(model.ToCommandRequest(orgId)))
            .ToResult(link => TypedResults.Created(
                $"organizations/{orgId}/invite-link",
                new OrganizationInviteLinkResponseModel(link)));

    public static async Task<IResult> UpdateAsync(
        [FromRoute] Guid orgId,
        [FromBody] UpdateOrganizationInviteLinkRequestModel model,
        IUpdateOrganizationInviteLinkCommand command) =>
        (await command.UpdateAsync(model.ToCommandRequest(orgId)))
            .ToResult(link => TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));

    public static async Task<IResult> UpdateInviteSupportConfirmAsync(
        [FromRoute] Guid orgId,
        [FromBody] UpdateInviteSupportConfirmRequestModel model,
        IUpdateInviteSupportConfirmCommand command) =>
        (await command.UpdateAsync(model.ToCommandRequest(orgId)))
            .ToResult(link => TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));

    public static async Task<IResult> DeleteAsync(
        [FromRoute] Guid orgId,
        IDeleteOrganizationInviteLinkCommand command) =>
        (await command.DeleteAsync(orgId)).ToResult();

    public static async Task<IResult> RefreshAsync(
        [FromRoute] Guid orgId,
        [FromBody] RefreshOrganizationInviteLinkRequestModel model,
        IRefreshOrganizationInviteLinkCommand command) =>
        (await command.RefreshAsync(model.ToCommandRequest(orgId)))
            .ToResult(link => TypedResults.Ok(new OrganizationInviteLinkResponseModel(link)));

    public static async Task<IResult> GetStatusAsync(
        [FromBody] GetOrganizationInviteLinkStatusRequestModel model,
        IGetOrganizationInviteLinkStatusQuery query) =>
        (await query.GetStatusAsync(model.OrganizationId, model.Code))
            .ToResult(status => TypedResults.Ok(status.ToResponseModel()));

    public static async Task<IResult> GetPoliciesAsync(
        [FromBody] GetOrganizationInviteLinkPoliciesRequestModel model,
        IGetOrganizationInviteLinkPoliciesQuery query) =>
        (await query.GetPoliciesAsync(model.OrganizationId, model.Code))
            .ToResult(policies => TypedResults.Ok(new ListResponseModel<PolicyResponseModel>(
                policies.Select(p => new PolicyResponseModel(p)))));

    public static async Task<IResult> ValidateEmailDomainAsync(
        [FromBody] OrganizationInviteLinkValidateEmailDomainRequestModel model,
        IValidateOrganizationInviteLinkQuery query) =>
        (await query.ValidateAsync(model.OrganizationId, model.Code, model.Email)).Match<IResult>(
            error => error switch
            {
                EmailDomainNotAllowed => TypedResults.Ok(new OrganizationInviteLinkValidateEmailDomainResponseModel(false)),
                _ => CommandErrorResults.ToResult(error),
            },
            _ => TypedResults.Ok(new OrganizationInviteLinkValidateEmailDomainResponseModel(true)));
}
