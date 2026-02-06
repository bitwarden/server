#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Billing.Attributes;

/// <summary>
/// An action filter that facilitates the injection of a <see cref="Organization"/> parameter into the executing action method arguments.
/// </summary>
/// <remarks>
/// <para>This attribute retrieves the organization associated with the 'organizationId' included in the executing context's route data. If the organization cannot be found,
/// the request is terminated with a not found response.</para>
/// <para>The injected <see cref="Organization"/>
/// parameter must be marked with a [BindNever] attribute to short-circuit the model-binding system.</para>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpPost]
/// [InjectOrganization]
/// public async Task<IResult> EndpointAsync([BindNever] Organization organization)
/// ]]></code>
/// </example>
/// <seealso cref="Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute"/>
public class InjectOrganizationAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.RouteData.Values.TryGetValue("organizationId", out var routeValue) ||
            !Guid.TryParse(routeValue?.ToString(), out var organizationId))
        {
            context.Result = new BadRequestObjectResult(new ErrorResponseModel("Route parameter 'organizationId' is missing or invalid."));
            return;
        }

        var organizationRepository = context.HttpContext.RequestServices
            .GetRequiredService<IOrganizationRepository>();

        var organization = await organizationRepository.GetByIdAsync(organizationId);

        if (organization == null)
        {
            context.Result = new NotFoundObjectResult(new ErrorResponseModel("Organization not found."));
            return;
        }

        var organizationParameter = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.ParameterType == typeof(Organization));

        if (organizationParameter != null)
        {
            context.ActionArguments[organizationParameter.Name] = organization;
        }

        await next();
    }
}
