#nullable enable
using Bit.Api.Models.Public.Response;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Billing.Attributes;

/// <summary>
/// An action filter that facilitates the injection of a <see cref="Provider"/> parameter into the executing action method arguments after performing an authorization check.
/// </summary>
/// <remarks>
/// <para>This attribute retrieves the provider associated with the 'providerId' included in the executing context's route data. If the provider cannot be found,
/// the request is terminated with a not-found response. It then checks the authorization level for the provider using the provided <paramref name="providerUserType"/>.
/// If this check fails, the request is terminated with an unauthorized response.</para>
/// <para>The injected <see cref="Provider"/>
/// parameter must be marked with a [BindNever] attribute to short-circuit the model-binding system.</para>
/// </remarks>
/// <example>
/// <code><![CDATA[
/// [HttpPost]
/// [InjectProvider(ProviderUserType.ProviderAdmin)]
/// public async Task<IResult> EndpointAsync([BindNever] Provider provider)
/// ]]></code>
/// </example>
/// <param name="providerUserType">The desired access level for the authorization check.</param>
/// <seealso cref="Microsoft.AspNetCore.Mvc.Filters.ActionFilterAttribute"/>
public class InjectProviderAttribute(ProviderUserType providerUserType) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (!context.RouteData.Values.TryGetValue("providerId", out var routeValue) ||
            !Guid.TryParse(routeValue?.ToString(), out var providerId))
        {
            context.Result = new BadRequestObjectResult(new ErrorResponseModel("Route parameter 'providerId' is missing or invalid."));
            return;
        }

        var providerRepository = context.HttpContext.RequestServices
            .GetRequiredService<IProviderRepository>();

        var provider = await providerRepository.GetByIdAsync(providerId);

        if (provider == null)
        {
            context.Result = new NotFoundObjectResult(new ErrorResponseModel("Provider not found."));
            return;
        }

        var currentContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentContext>();

        var unauthorized = providerUserType switch
        {
            ProviderUserType.ProviderAdmin => !currentContext.ProviderProviderAdmin(providerId),
            ProviderUserType.ServiceUser => !currentContext.ProviderUser(providerId),
            _ => false
        };

        if (unauthorized)
        {
            context.Result = new UnauthorizedObjectResult(new ErrorResponseModel("Unauthorized."));
            return;
        }

        var providerParameter = context.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.ParameterType == typeof(Provider));

        if (providerParameter != null)
        {
            context.ActionArguments[providerParameter.Name] = provider;
        }

        await next();
    }
}
