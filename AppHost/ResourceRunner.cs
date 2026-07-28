using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.AppHost;

/// <summary>
/// Runs a run-to-completion resource (an executable that does a job and exits) from inside a resource
/// command handler, and waits for it to finish.
/// </summary>
/// <remarks>
/// Only ever issues <c>start</c>, never <c>restart</c>. Restarting a resource tears down the command
/// handlers attached to that same resource: the handler stops dead where it is, with no exception and
/// no completion, silently stranding whatever it meant to do next. <c>start</c> stops nothing, so the
/// handler survives.
/// </remarks>
internal static class ResourceRunner
{
    /// <summary>How long to watch for the resource leaving its terminal state before giving up on it.</summary>
    private static readonly TimeSpan s_startupWindow = TimeSpan.FromSeconds(30);

    private static readonly string[] s_runningStates = [KnownResourceStates.Starting, KnownResourceStates.Running];

    private static readonly string[] s_terminalStates =
        [KnownResourceStates.Finished, KnownResourceStates.Exited, KnownResourceStates.FailedToStart];

    /// <summary>
    /// Starts <paramref name="resourceName"/> and waits for the new run to exit, returning its final
    /// state. Throws if the resource cannot be started.
    /// </summary>
    public static async Task<string> RunToCompletionAsync(
        IServiceProvider services,
        string resourceName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var commands = services.GetRequiredService<ResourceCommandService>();
        var notifications = services.GetRequiredService<ResourceNotificationService>();

        var start = await commands.ExecuteCommandAsync(
            resourceName, KnownResourceCommands.StartCommand, cancellationToken);

        if (!start.Success)
        {
            // Almost always because a run is already in flight. Let that one finish and start a fresh
            // one, rather than reaching for restart and killing this handler.
            logger.LogInformation(
                "'{Resource}' would not start ({Error}); waiting for the run in progress.",
                resourceName, start.ErrorMessage);

            await WaitForExitAsync(notifications, resourceName, cancellationToken);

            start = await commands.ExecuteCommandAsync(
                resourceName, KnownResourceCommands.StartCommand, cancellationToken);

            if (!start.Success)
            {
                throw new InvalidOperationException($"Could not start '{resourceName}': {start.ErrorMessage}");
            }
        }

        await WaitForStartAsync(notifications, resourceName, cancellationToken);
        return await WaitForExitAsync(notifications, resourceName, cancellationToken);
    }

    /// <summary>
    /// Waits for the resource to leave its terminal state. Without this the exit wait would match the
    /// <em>previous</em> run's terminal state and return immediately, reporting success before the new
    /// run has done anything.
    /// </summary>
    private static async Task WaitForStartAsync(
        ResourceNotificationService notifications,
        string resourceName,
        CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(s_startupWindow);

        try
        {
            await notifications.WaitForResourceAsync(resourceName, s_runningStates, startup.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A short-lived script can start and exit inside the polling window; fall through to the
            // exit wait, which will then match the run we just started.
        }
    }

    private static Task<string> WaitForExitAsync(
        ResourceNotificationService notifications,
        string resourceName,
        CancellationToken cancellationToken) =>
        notifications.WaitForResourceAsync(resourceName, s_terminalStates, cancellationToken);
}
