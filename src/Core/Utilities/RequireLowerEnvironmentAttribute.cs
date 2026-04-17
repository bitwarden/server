using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Hosting;

namespace Bit.Core.Utilities;

/// <summary>
/// Authorization attribute that restricts controller/action access to Development and QA environments only.
/// Returns 404 Not Found in all other environments.
/// </summary>
public class RequireLowerEnvironmentAttribute() : TypeFilterAttribute(typeof(LowerEnvironmentFilter))
{
    private class LowerEnvironmentFilter(IWebHostEnvironment environment) : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment("QA"))
            {
                context.Result = new NotFoundResult();
            }
        }
    }
}
