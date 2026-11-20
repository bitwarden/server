using Bit.Core.Auth.AgentFeatures.Commands;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.IdentityServer;
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
public class CreateAgentConnectionCommandTests
{
    private static OpenIdConnectPartner Partner(string clientId = "partner.acme") => new()
    {
        ClientId = clientId,
        Name = "Acme",
        ClientSecretHash = "hash",
        RedirectUris = ["https://vault.example.com/oauth/complete"],
    };

    [Theory, BitAutoData]
    public async Task CreateAsync_RegisteredPartner_StoresEncryptedKeyAndPushes(
        SutProvider<CreateAgentConnectionCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IOpenIdConnectPartnerStore>()
            .GetByClientId("partner.acme").Returns(Partner());
        sutProvider.GetDependency<IPushNotificationService>()
            .PushAsync(Arg.Any<PushNotification<SyncAgentConnectionPushNotification>>())
            .Returns(Task.CompletedTask);

        var result = await sutProvider.Sut.CreateAsync(userId, "partner.acme", "My bot", "2.encryptedPsk==");

        Assert.Equal(userId, result.UserId);
        Assert.Equal("partner.acme", result.PartnerClientId);
        Assert.Equal("My bot", result.Name);
        Assert.Equal("2.encryptedPsk==", result.Key);
        Assert.NotEqual(Guid.Empty, result.Id);

        await sutProvider.GetDependency<IAgentConnectionRepository>()
            .Received(1).CreateAsync(Arg.Is<AgentConnection>(c => c.UserId == userId && c.Key == "2.encryptedPsk=="));
        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1).PushAsync(Arg.Is<PushNotification<SyncAgentConnectionPushNotification>>(
                p => p.Type == PushType.SyncAgentConnectionCreate && p.TargetId == userId));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_UnknownPartner_Throws(
        SutProvider<CreateAgentConnectionCommand> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IOpenIdConnectPartnerStore>()
            .GetByClientId(Arg.Any<string>()).Returns((OpenIdConnectPartner)null);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.CreateAsync(userId, "partner.unknown", "x", "2.key=="));

        await sutProvider.GetDependency<IAgentConnectionRepository>()
            .DidNotReceiveWithAnyArgs().CreateAsync(default);
    }
}
