#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DiagnosticAdapter;

namespace Bit.SharedWeb.Health;

public class MiddlewareAnalysisDiagnosticAdapter
{
    private readonly ActivitySource _activitySource;
    private readonly ConcurrentDictionary<string, Activity> _activities = new();

    public MiddlewareAnalysisDiagnosticAdapter(ActivitySource activitySource)
    {
        _activitySource = activitySource;
    }

    [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareStarting")]
    public void OnMiddlewareStarting(HttpContext httpContext, string name, Guid instance, long timestamp)
    {
        if (name == "Microsoft.AspNetCore.MiddlewareAnalysis.AnalysisMiddleware")
        {
            return;
        }

        if (_activitySource.StartActivity(name, ActivityKind.Server) is Activity activity)
        {
            _activities[name] = activity;
        }
    }

    [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareException")]
    public void OnMiddlewareException(Exception exception, HttpContext httpContext, string name, Guid instance, long timestamp, long duration)
    {
        if (name == "Microsoft.AspNetCore.MiddlewareAnalysis.AnalysisMiddleware" || !_activities.ContainsKey(name))
        {
            return;
        }
        _activities[name].AddTag("exception", exception.ToString());
        _activities[name].AddTag("exceptionStackTrace", exception.StackTrace);

        _activities[name].Stop();
    }

    [DiagnosticName("Microsoft.AspNetCore.MiddlewareAnalysis.MiddlewareFinished")]
    public void OnMiddlewareFinished(HttpContext httpContext, string name, Guid instance, long timestamp, long duration)
    {
        if (name == "Microsoft.AspNetCore.MiddlewareAnalysis.AnalysisMiddleware" || !_activities.ContainsKey(name))
        {
            return;
        }
        _activities[name].Stop();
    }

}
