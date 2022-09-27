using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Utilities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SecretsManagerAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var isDev = context.HttpContext.RequestServices.GetService<IHostEnvironment>().IsDevelopment();
        var isEE = Environment.GetEnvironmentVariable("EE_TESTING_ENV") != null;
        if (!isDev && !isEE)
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}

