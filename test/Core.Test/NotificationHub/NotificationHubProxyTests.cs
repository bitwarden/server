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
            (NotificationHubClientProxy c) => c.DeleteInstallationAsync("test"),
            (INotificationHubClient c) => c.DeleteInstallationAsync("test"),
        ],
        [
            (NotificationHubClientProxy c) => c.DeleteInstallationAsync("test", default),
            (INotificationHubClient c) => c.DeleteInstallationAsync("test", default),
        ],
        [
            (NotificationHubClientProxy c) => c.PatchInstallationAsync("test", new List<PartialUpdateOperation>()),
            (INotificationHubClient c) => c.PatchInstallationAsync("test", Arg.Any<List<PartialUpdateOperation>>()),
        ],
        [
            (NotificationHubClientProxy c) => c.PatchInstallationAsync("test", new List<PartialUpdateOperation>(), default),
            (INotificationHubClient c) => c.PatchInstallationAsync("test", Arg.Any<List<PartialUpdateOperation>>(), default)
        ]
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
