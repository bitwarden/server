using System.Text.Json;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

[SutProviderCustomize]
public class ItemSendCleanupServiceTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteItemSendsForCiphersAsync_DeletesOnlySendsReferencingGivenCiphers(
        Guid cipherId, Guid otherCipherId, SutProvider<ItemSendCleanupService> sutProvider)
    {
        var matchingSend = CreateItemSend(cipherId);
        var otherItemSend = CreateItemSend(otherCipherId);
        var malformedSend = CreateItemSend(cipherId);
        malformedSend.Data = "{ \"metadata\": ";
        sutProvider.GetDependency<ISendRepository>()
            .GetManyItemSendsByItemIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Send> { matchingSend, otherItemSend, malformedSend });

        await sutProvider.Sut.DeleteItemSendsForCiphersAsync([cipherId]);

        await sutProvider.GetDependency<INonAnonymousSendCommand>().Received(1).DeleteSendAsync(matchingSend);
        await sutProvider.GetDependency<INonAnonymousSendCommand>().DidNotReceive().DeleteSendAsync(otherItemSend);
        await sutProvider.GetDependency<INonAnonymousSendCommand>().DidNotReceive().DeleteSendAsync(malformedSend);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteItemSendsForCiphersAsync_WithManyCiphers_LooksUpInBatches(
        SutProvider<ItemSendCleanupService> sutProvider)
    {
        var cipherIds = Enumerable.Range(0, 1200).Select(_ => Guid.NewGuid()).ToList();
        sutProvider.GetDependency<ISendRepository>()
            .GetManyItemSendsByItemIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Send>());

        await sutProvider.Sut.DeleteItemSendsForCiphersAsync(cipherIds);

        await sutProvider.GetDependency<ISendRepository>().Received(3)
            .GetManyItemSendsByItemIdsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteItemSendsForCiphersAsync_WithNoCiphers_DoesNotQuery(
        SutProvider<ItemSendCleanupService> sutProvider)
    {
        await sutProvider.Sut.DeleteItemSendsForCiphersAsync([]);

        await sutProvider.GetDependency<ISendRepository>().DidNotReceiveWithAnyArgs()
            .GetManyItemSendsByItemIdsAsync(default);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteItemSendsForOrganizationAsync_DeletesSendsForEveryOrganizationCipher(
        Guid organizationId, Guid cipherId, Guid otherCipherId, SutProvider<ItemSendCleanupService> sutProvider)
    {
        var ciphers = new List<Cipher>
        {
            new() { Id = cipherId, OrganizationId = organizationId },
            new() { Id = otherCipherId, OrganizationId = organizationId },
        };
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(ciphers);
        var send = CreateItemSend(otherCipherId);
        sutProvider.GetDependency<ISendRepository>()
            .GetManyItemSendsByItemIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Send> { send });

        await sutProvider.Sut.DeleteItemSendsForOrganizationAsync(organizationId);

        await sutProvider.GetDependency<INonAnonymousSendCommand>().Received(1).DeleteSendAsync(send);
    }

    private static Send CreateItemSend(Guid itemId) => new()
    {
        Id = Guid.NewGuid(),
        Type = SendType.Item,
        Data = JsonSerializer.Serialize(new SendItemData
        {
            Metadata = new SendItemMetadata { ItemId = itemId },
        }),
    };
}
