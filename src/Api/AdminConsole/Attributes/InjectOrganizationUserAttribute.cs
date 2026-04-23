using Bit.Api.AdminConsole.Authorization;
using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.AdminConsole.Attributes;

/// <summary>
/// Validates that the specified organization user belongs to the organization identified by the
/// <c>orgId</c> or <c>organizationId</c> route parameter, and optionally injects the loaded
/// <see cref="OrganizationUser"/> into the action method arguments.
/// </summary>
/// <remarks>
/// <para>The organization user is resolved from the route parameter named by
/// <paramref name="organizationUserIdRouteParam"/> (default <c>"id"</c>). Its
/// <see cref="OrganizationUser.OrganizationId"/> must match the organization route value.
/// If validation fails, the request is short-circuited with an appropriate error response.</para>
/// <para>The injected <see cref="OrganizationUser"/> parameter must be marked with
/// <c>[BindNever]</c> to bypass model binding.</para>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpGet("{id}")]
/// [InjectOrganizationUser]
/// public async Task<IResult> GetAsync(Guid id, [BindNever] OrganizationUser organizationUser)
///
/// [HttpPost("{organizationUserId}/accept")]
/// [InjectOrganizationUser("organizationUserId")]
/// public async Task<IResult> AcceptAsync(Guid organizationUserId, [BindNever] OrganizationUser organizationUser)
/// ]]></code>
/// </example>
/// <param name="organizationUserIdRouteParam">
/// Name of the route parameter containing the organization user ID. Defaults to <c>"id"</c>.
/// </param>
public class InjectOrganizationUserAttribute(string organizationUserIdRouteParam = "id") : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        Guid orgId;
        try
        {
            orgId = context.HttpContext.GetOrganizationId();
        }
        catch (InvalidOperationException)
        {
            context.Result = new BadRequestObjectResult(
                new ErrorResponseModel("Route parameter 'orgId' or 'organizationId' is missing or invalid."));
            return;
        }

        if (!context.RouteData.Values.TryGetValue(organizationUserIdRouteParam, out var orgUserIdRouteValue) ||
            !Guid.TryParse(orgUserIdRouteValue?.ToString(), out var orgUserId))
        {
            context.Result = new BadRequestObjectResult(
                new ErrorResponseModel($"Route parameter '{organizationUserIdRouteParam}' is missing or invalid."));
            return;
        }

        var organizationUserRepository = context.HttpContext.RequestServices
            .GetRequiredService<IOrganizationUserRepository>();

        var organizationUser = await organizationUserRepository.GetByIdAsync(orgUserId);

        if (organizationUser == null || organizationUser.OrganizationId != orgId)
        {
            context.Result = new NotFoundObjectResult(
                new ErrorResponseModel("Organization user not found."));
            return;
        }

        var organizationUserParameter = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.ParameterType == typeof(OrganizationUser));

        if (organizationUserParameter != null)
        {
            context.ActionArguments[organizationUserParameter.Name] = organizationUser;
        }

        await next();
    }
}
