using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace Notifications.Test;

/// <summary>
/// A single notification handed to SignalR, captured before it reaches a real hub lifetime manager.
/// </summary>
/// <param name="Hub">The name of the hub type the notification was sent through.</param>
/// <param name="Destination">
/// The routing destination, formatted as <c>User:{userId}</c> or <c>Group:{groupName}</c>.
/// </param>
/// <param name="Method">The client method name, e.g. <c>ReceiveMessage</c>.</param>
/// <param name="Arguments">The arguments passed to the client method.</param>
internal sealed record HubInvocation(string Hub, string Destination, string Method, object?[] Arguments);

/// <summary>
/// Builds substitute <see cref="IHubContext{THub}"/> instances that record every notification sent
/// through them. All contexts created by one recorder share a single queue, so a test can await the
/// next notification without knowing which hub it will be routed to.
///
/// <para>Recording the arguments — rather than only asserting which user or group was targeted —
/// lets a test re-encode the notification with the real SignalR protocol and assert on the bytes
/// that would have gone out over the wire.</para>
/// </summary>
internal sealed class HubInvocationRecorder
{
    private readonly Channel<HubInvocation> _invocations = Channel.CreateUnbounded<HubInvocation>(
        new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

    // One proxy per destination, so repeated sends to the same user or group are recorded by the
    // same substitute and Received() assertions on it stay meaningful.
    private readonly ConcurrentDictionary<string, IClientProxy> _proxies = new();

    /// <summary>
    /// Creates a substitute <see cref="IHubContext{THub}"/> whose <see cref="IHubClients.User"/> and
    /// <see cref="IHubClients.Group"/> proxies record into this recorder.
    /// </summary>
    public (IHubContext<THub> Context, IHubClients Clients) CreateHubContext<THub>()
        where THub : Hub
    {
        var hubName = typeof(THub).Name;

        var clients = Substitute.For<IHubClients>();
        clients.User(Arg.Any<string>()).Returns(call => GetProxy(hubName, $"User:{call.ArgAt<string>(0)}"));
        clients.Group(Arg.Any<string>()).Returns(call => GetProxy(hubName, $"Group:{call.ArgAt<string>(0)}"));

        var context = Substitute.For<IHubContext<THub>>();
        context.Clients.Returns(clients);
        return (context, clients);
    }

    /// <summary>
    /// Drops every notification recorded so far.
    /// </summary>
    public void DiscardRecorded()
    {
        while (_invocations.Reader.TryRead(out _))
        {
            // Reading is the discard; there is nothing to do with what comes out.
        }
    }

    /// <summary>
    /// Waits for the next notification sent through any hub context created by this recorder.
    /// </summary>
    public async Task<HubInvocation> AwaitNextAsync(CancellationToken cancellationToken = default)
        => await _invocations.Reader.ReadAsync(cancellationToken);

    private IClientProxy GetProxy(string hubName, string destination)
        => _proxies.GetOrAdd($"{hubName}/{destination}", _ =>
        {
            var proxy = Substitute.For<IClientProxy>();
            proxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    _invocations.Writer.TryWrite(new HubInvocation(
                        hubName, destination, call.ArgAt<string>(0), call.ArgAt<object?[]>(1)));
                    return Task.CompletedTask;
                });
            return proxy;
        });
}
