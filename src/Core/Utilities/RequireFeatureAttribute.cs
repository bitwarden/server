using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

/// <summary>
/// Specifies that the class or method that this attribute is applied to requires the specified boolean feature flag
/// to be enabled. If the feature flag is not enabled, a <see cref="FeatureUnavailableException"/> is thrown
/// </summary>
public class RequireFeatureAttribute : ActionFilterAttribute
{
    private readonly string _featureFlagKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequireFeatureAttribute"/> class with the specified feature flag key.
    /// </summary>
    /// <param name="featureFlagKey">The name of the feature flag to require.</param>
    public RequireFeatureAttribute(string featureFlagKey)
    {
        _featureFlagKey = featureFlagKey;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var currentContext = context.HttpContext.RequestServices.GetRequiredService<ICurrentContext>();
        var featureService = context.HttpContext.RequestServices.GetRequiredService<IFeatureService>();

        if (!featureService.IsEnabled(_featureFlagKey, currentContext))
        {
            throw new FeatureUnavailableException();
        }
    }
}
