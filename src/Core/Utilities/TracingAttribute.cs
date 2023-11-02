#nullable enable

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Utilities;

public class TracingAttribute : ActionFilterAttribute
{
    private Activity? _actionActivity;
    private Activity? _resultActivity;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var activitySource = context.HttpContext.RequestServices.GetService<ActivitySource>();

        if (activitySource == null)
        {
            return;
        }

        var activityName = $"{context.ActionDescriptor.DisplayName}_action";
        _actionActivity = activitySource.StartActivity(activityName, ActivityKind.Server);
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (_actionActivity == null)
        {
            return;
        }

        if (context.Exception != null)
        {
            _actionActivity.AddTag("exception", context.Exception.ToString());
            _actionActivity.AddTag("exceptionStackTrace", context.Exception.StackTrace);
        }

        _actionActivity.Stop();
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        var activitySource = context.HttpContext.RequestServices.GetService<ActivitySource>();

        if (activitySource == null)
        {
            return;
        }

        var activityName = $"{context.ActionDescriptor.DisplayName}_result";
        _resultActivity = activitySource.StartActivity(activityName, ActivityKind.Server);
    }

    public override void OnResultExecuted(ResultExecutedContext context)
    {
        if (_resultActivity == null)
        {
            return;
        }

        if (context.Exception != null)
        {
            _resultActivity.AddTag("exception", context.Exception.ToString());
            _resultActivity.AddTag("exceptionStackTrace", context.Exception.StackTrace);
        }

        _resultActivity.Stop();
    }
}
