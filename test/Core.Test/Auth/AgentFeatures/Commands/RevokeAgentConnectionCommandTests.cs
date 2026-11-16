using Bit.Core.Auth.AgentFeatures.Commands;
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

namespace Bit.Core.Test.Auth.AgentFeatures.Commands;

[SutProviderCustomize]
public class RevokeAgentConnectionCommandTests
{
    [Theory, BitAutoData]
    public async Task RevokeAsync_Owner_DeletesAndPushes(
        SutProvider<RevokeAgentConnectionCommand> sutProvider, AgentConnection connection)
    {
        sutProvider.GetDependency<IAgentConnectionRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);
        sutProvider.GetDependency<IPushNotificationService>()
            .PushAsync(Arg.Any<PushNotification<SyncAgentConnectionPushNotification>>())
            .Returns(Task.CompletedTask);

        await sutProvider.Sut.RevokeAsync(connection.UserId, connection.Id);

        await sutProvider.GetDependency<IAgentConnectionRepository>().Received(1).DeleteAsync(connection);
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1).PushAsync(Arg.Is<PushNotification<SyncAgentConnectionPushNotification>>(
                p => p.Type == PushType.SyncAgentConnectionDelete && p.TargetId == connection.UserId));
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_NotOwner_Throws(
        SutProvider<RevokeAgentConnectionCommand> sutProvider, AgentConnection connection, Guid otherUserId)
    {
        sutProvider.GetDependency<IAgentConnectionRepository>()
            .GetByIdAsync(connection.Id).Returns(connection);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sutProvider.Sut.RevokeAsync(otherUserId, connection.Id));

        await sutProvider.GetDependency<IAgentConnectionRepository>()
            .DidNotReceiveWithAnyArgs().DeleteAsync(default);
    }

    [Theory, BitAutoData]
    public async Task RevokeAsync_Missing_Throws(
        SutProvider<RevokeAgentConnectionCommand> sutProvider, Guid userId, Guid connectionId)
    {
        sutProvider.GetDependency<IAgentConnectionRepository>()
            .GetByIdAsync(connectionId).Returns((AgentConnection)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.RevokeAsync(userId, connectionId));
    }
}
