#nullable enable

using System.Diagnostics;
using Bit.SharedWeb.Health;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.SharedWeb.Utilities;

public static class DiagnosticsHelpers
{
    public static void AddMiddlewareDiagnostics(IServiceProvider serviceProvider)
    {
        var listener = serviceProvider.GetRequiredService<DiagnosticListener>();
        var activitySource = serviceProvider.GetRequiredService<ActivitySource>();

        listener.SubscribeWithAdapter(new MiddlewareAnalysisDiagnosticAdapter(activitySource));
    }
}
