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
public class UpdateConnectedAppCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateAsync_Owner_RenamesAndPushes(
        SutProvider<UpdateConnectedAppCommand> sutProvider, ConnectedApp connection)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);

        var result = await sutProvider.Sut.UpdateAsync(connection.UserId, connection.Id, "Renamed");

        Assert.Equal("Renamed", result.Name);
        await sutProvider.GetDependency<IConnectedAppRepository>()
            .Received(1).ReplaceAsync(Arg.Is<ConnectedApp>(c => c.Id == connection.Id && c.Name == "Renamed"));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1).PushAsync(Arg.Is<PushNotification<SyncConnectedAppPushNotification>>(
                p => p.Type == PushType.SyncConnectedAppUpdate && p.TargetId == connection.UserId));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_NotOwner_Throws(
        SutProvider<UpdateConnectedAppCommand> sutProvider, ConnectedApp connection, Guid otherUserId)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAsync(otherUserId, connection.Id, "x"));

        await sutProvider.GetDependency<IConnectedAppRepository>()
            .DidNotReceiveWithAnyArgs().ReplaceAsync(default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_Missing_Throws(
        SutProvider<UpdateConnectedAppCommand> sutProvider, Guid userId, Guid connectionId)
    {
        sutProvider.GetDependency<IConnectedAppRepository>()
            .GetByIdAsync(connectionId).Returns((ConnectedApp)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.UpdateAsync(userId, connectionId, "x"));
    }
}
