using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Bit.Api.Utilities;

/// <summary>
/// An <see cref="IActionModelConvention"/> that rewrites the route template for actions
/// decorated with <see cref="VersionedRouteAttribute"/> to an absolute versioned path.
/// <para>
/// ASP.NET Core treats route templates starting with <c>/</c> as absolute, bypassing the
/// controller's <c>[Route]</c> prefix.  This convention reads the controller route, prepends
/// <c>v{N}/</c>, and sets an absolute route on each of the action's selectors.
/// </para>
/// </summary>
public class VersionedRouteConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var versionAttr = action.ActionMethod.GetCustomAttribute<VersionedRouteAttribute>();
        if (versionAttr is null)
        {
            return;
        }

        var controllerRouteTemplate = action.Controller.Selectors
            .Select(s => s.AttributeRouteModel?.Template)
            .FirstOrDefault(t => !string.IsNullOrEmpty(t));

        if (controllerRouteTemplate is null)
        {
            throw new InvalidOperationException(
                $"Controller '{action.Controller.ControllerType.FullName}' must have a [Route] attribute " +
                $"to use [VersionedRoute] on action '{action.ActionMethod.Name}'.");
        }

        var versionPrefix = $"v{versionAttr.Version}";

        foreach (var selector in action.Selectors)
        {
            var actionRoute = selector.AttributeRouteModel?.Template;

            var combinedRoute = string.IsNullOrEmpty(actionRoute)
                ? $"/{versionPrefix}/{controllerRouteTemplate}"
                : $"/{versionPrefix}/{controllerRouteTemplate}/{actionRoute}";

            if (selector.AttributeRouteModel is null)
            {
                selector.AttributeRouteModel = new AttributeRouteModel { Template = combinedRoute };
            }
            else
            {
                selector.AttributeRouteModel.Template = combinedRoute;
            }
        }
    }
}
