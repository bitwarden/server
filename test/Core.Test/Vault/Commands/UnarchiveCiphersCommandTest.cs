using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[UserCipherCustomize]
[SutProviderCustomize]
public class UnarchiveCiphersCommandTest
{
    [Theory]
    [BitAutoData(true, false, 1, 1, 1, 1)]
    [BitAutoData(false, false, 1, 0, 0, 1)]
    [BitAutoData(false, true, 1, 0, 0, 1)]
    [BitAutoData(true, true, 1, 1, 1, 1)]
    public async Task UnarchiveAsync_Works(
        bool isEditable, bool hasOrganizationId,
        int cipherRepoCalls, int resultCountFromQuery, int eventServiceCalls, int pushNotificationsCalls,
        SutProvider<UnarchiveCiphersCommand> sutProvider, CipherDetails cipher, User user)
    {
        cipher.Edit = isEditable;
        cipher.OrganizationId = hasOrganizationId ? Guid.NewGuid() : null;

        var cipherList = new List<CipherDetails> { cipher };

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id).Returns(cipherList);

        // Act
        await sutProvider.Sut.UnarchiveManyAsync([cipher.Id], user.Id);

        // Assert
        await sutProvider.GetDependency<ICipherRepository>().Received(cipherRepoCalls).UnarchiveAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Count() == resultCountFromQuery
                                             && ids.Count() >= 1 ? true : ids.All(id => cipherList.Contains(cipher))),
            user.Id);
        await sutProvider.GetDependency<IEventService>().Received(eventServiceCalls)
            .LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(pushNotificationsCalls)
            .PushSyncCiphersAsync(user.Id);
    }
}
