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
public class ArchiveCiphersCommandTest
{
    [Theory]
    [BitAutoData(true, false, 1, 1, 1)]
    [BitAutoData(false, false, 1, 0, 1)]
    [BitAutoData(false, true, 1, 0, 1)]
    [BitAutoData(true, true, 1, 0, 1)]
    public async Task ArchiveAsync_Works(
        bool isEditable, bool hasOrganizationId,
        int cipherRepoCalls, int eventServiceCalls, int pushNotificationsCalls,
        SutProvider<ArchiveCiphersCommand> sutProvider, CipherDetails cipher, User user)
    {
        // Arrange
        cipher.Edit = isEditable;
        cipher.OrganizationId = hasOrganizationId ? Guid.NewGuid() : null;

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id).Returns(new List<CipherDetails> { cipher });

        // Act
        await sutProvider.Sut.ArchiveManyAsync([cipher.Id], user.Id);

        // Assert
        await sutProvider.GetDependency<ICipherRepository>().Received(cipherRepoCalls).ArchiveAsync(Arg.Any<IEnumerable<Guid>>(), user.Id);
        await sutProvider.GetDependency<IEventService>().Received(eventServiceCalls).LogCipherEventsAsync(Arg.Any<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(pushNotificationsCalls).PushSyncCiphersAsync(user.Id);
    }
}
