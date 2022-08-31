using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Bit.Api.Utilities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SecretsManagerAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var env = context.HttpContext.RequestServices.GetService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            context.Result = new NotFoundResult();
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context) { }
}

