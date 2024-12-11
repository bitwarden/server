using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Utilities;

public class LoggingExceptionHandlerFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        var exception = context.Exception;
        if (exception == null)
        {
            // Should never happen.
            return;
        }

        var logger = context.HttpContext.RequestServices.GetRequiredService<
            ILogger<LoggingExceptionHandlerFilterAttribute>
        >();
        logger.LogError(0, exception, exception.Message);
    }
}
