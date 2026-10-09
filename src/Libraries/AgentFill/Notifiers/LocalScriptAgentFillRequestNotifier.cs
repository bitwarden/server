using System.ComponentModel;
using System.Diagnostics;
using Bit.AgentFill.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.AgentFill.Notifiers;

/// <summary>
/// Development only. Runs a local script, such as the iOS repository's <c>Scripts/simulate-agent-fill-push.sh</c>,
/// instead of sending a push through APNS, so no push configuration is needed. The script receives the approval ID
/// and the user ID as arguments; both are GUIDs, never sealed content. A failing script is logged and never fails the
/// request, because the approval is already stored.
/// </summary>
internal sealed class LocalScriptAgentFillRequestNotifier(
    string scriptPath,
    ILogger<LocalScriptAgentFillRequestNotifier> logger) : IAgentFillRequestNotifier
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public async Task NotifyAsync(AgentFillApprovalRequest request)
    {
        var startInfo = new ProcessStartInfo(scriptPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(request.Id.ToString());
        startInfo.ArgumentList.Add(request.UserId.ToString());

        try
        {
            using var process = Process.Start(startInfo)!;
            using var cts = new CancellationTokenSource(_timeout);
            var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderr = process.StandardError.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
            await Task.WhenAll(stdout, stderr);

            if (process.ExitCode == 0)
            {
                logger.LogInformation("Agent fill approval {ApprovalId}: {Outcome}", request.Id, "SimulatedPushSent");
            }
            else
            {
                logger.LogWarning("Agent fill approval {ApprovalId}: simulate push script exited with {ExitCode}: {Error}",
                    request.Id, process.ExitCode, stderr.Result.Trim());
            }
        }
        catch (Exception ex) when (ex is Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Agent fill approval {ApprovalId}: could not run the simulate push script",
                request.Id);
        }
    }
}
