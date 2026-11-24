using Bit.Core.Auth.ConnectedApps.Commands;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models;
using Bit.Core.Auth.Repositories;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.ConnectedApps.Commands;

[SutProviderCustomize]
public class RevokeConnectedAppCommandTests
{
    [Theory, BitAutoData]
    public async Task RevokeAsync_Owner_DeletesAndPushes(
        SutProvider<RevokeConnectedAppCommand> sutProvider, ConnectedApp connection)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);
        sutProvider.GetDependency<IPushNotificationService>()
            .PushAsync(Arg.Any<PushNotification<SyncConnectedAppPushNotification>>())
            .Returns(Task.CompletedTask);

        await sutProvider.Sut.RevokeAsync(connection.UserId, connection.Id);

        await sutProvider.GetDependency<IConnectedAppRepository>().Received(1).DeleteAsync(connection);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1).PushAsync(Arg.Is<PushNotification<SyncConnectedAppPushNotification>>(
                p => p.Type == PushType.SyncConnectedAppDelete && p.TargetId == connection.UserId));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotOwner_Throws(
        SutProvider<RevokeConnectedAppCommand> sutProvider, ConnectedApp connection, Guid otherUserId)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RevokeAsync(otherUserId, connection.Id));

        await sutProvider.GetDependency<IConnectedAppRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Missing_Throws(
        SutProvider<RevokeConnectedAppCommand> sutProvider, Guid userId, Guid connectionId)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connectionId).Returns((ConnectedApp)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, connectionId));
    }
}
