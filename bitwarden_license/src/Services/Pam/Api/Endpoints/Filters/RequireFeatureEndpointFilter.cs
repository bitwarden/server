using Bit.Core.Exceptions;
using Bitwarden.Server.Sdk.Features;

namespace Bit.Services.Pam.Api.Endpoints.Filters;

/// <summary>
/// Minimal API equivalent of <see cref="Bit.Core.Utilities.RequireFeatureAttribute"/>: gates an endpoint group
/// behind a boolean feature flag. When the flag is disabled a <see cref="FeatureUnavailableException"/> (a
/// <see cref="NotFoundException"/>) is thrown, which <see cref="PamExceptionHandlerEndpointFilter"/> renders as a 404.
/// </summary>
public class RequireFeatureEndpointFilter(string featureFlagKey) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var featureService = context.HttpContext.RequestServices.GetRequiredService<IFeatureService>();
        if (!featureService.IsEnabled(featureFlagKey))
        {
            throw new FeatureUnavailableException();
        }

        return await next(context);
    }
}
