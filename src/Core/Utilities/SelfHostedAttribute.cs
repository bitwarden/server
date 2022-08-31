using Bit.Core.Exceptions;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

public class SelfHostedAttribute : ActionFilterAttribute
{
    public bool SelfHostedOnly { get; set; }
    public bool NotSelfHostedOnly { get; set; }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var globalSettings = context.HttpContext.RequestServices.GetRequiredService<GlobalSettings>();
        if (SelfHostedOnly && !globalSettings.SelfHosted)
        {
            throw new BadRequestException("Only allowed when self hosted.");
        }
        else if (NotSelfHostedOnly && globalSettings.SelfHosted)
        {
            throw new BadRequestException("Only allowed when not self hosted.");
        }
    }
}
