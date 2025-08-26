using AutoFixture;
using Bit.Core.NotificationHub;
using Bit.Test.Common.AutoFixture;
using Microsoft.Azure.NotificationHubs;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.NotificationHub;

public class NotificationHubProxyTests
{
    private readonly IEnumerable<INotificationHubClient> _clients;
    public NotificationHubProxyTests()
    {
        _clients = new Fixture().WithAutoNSubstitutions().CreateMany<INotificationHubClient>();
    }

    public static IEnumerable<object[]> ClientMethods =
    [
        [
            (NotificationHubClientProxy c) => c.SendTemplateNotificationAsync(new Dictionary<string, string>() { { "key", "value" } }, "tag"),
            (INotificationHubClient c) => c.SendTemplateNotificationAsync(Arg.Is<Dictionary<string, string>>((a) => a.Keys.Count == 1 && a.ContainsKey("key") && a["key"] == "value"), "tag"),
        ],
    ];

    [Theory]
    [MemberData(nameof(ClientMethods))]
    public async void CallsAllClients(Func<NotificationHubClientProxy, Task> proxyMethod, Func<INotificationHubClient, Task> clientMethod)
    {
        var clients = _clients.ToArray();
        var proxy = new NotificationHubClientProxy(clients);

        await proxyMethod(proxy);

        foreach (var client in clients)
        {
            await clientMethod(client.Received());
        }
    }
}
