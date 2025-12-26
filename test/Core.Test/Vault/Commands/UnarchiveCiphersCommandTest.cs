using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Test.AutoFixture.CipherFixtures;
using Bit.Core.Vault.Commands;
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
    [BitAutoData(true, false, 1, 1, 1)]
    [BitAutoData(false, false, 1, 0, 1)]
    [BitAutoData(false, true, 1, 0, 1)]
    [BitAutoData(true, true, 1, 1, 1)]
    public async Task UnarchiveAsync_Works(
        bool isEditable, bool hasOrganizationId,
        int cipherRepoCalls, int resultCountFromQuery, int pushNotificationsCalls,
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
                                             && ids.Count() >= 1
                ? true
                : ids.All(id => cipherList.Contains(cipher))),
            user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().Received(pushNotificationsCalls)
            .PushSyncCiphersAsync(user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task UnarchiveAsync_ClearsArchivedDateOnReturnedCiphers(
        SutProvider<UnarchiveCiphersCommand> sutProvider,
        CipherDetails cipher,
        User user)
    {
        // Arrange: make it unarchivable
        cipher.Edit = true;
        cipher.OrganizationId = null;
        cipher.ArchivedDate = DateTime.UtcNow;

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<CipherDetails> { cipher });

        var repoRevisionDate = DateTime.UtcNow.AddMinutes(1);

        sutProvider.GetDependency<ICipherRepository>()
            .UnarchiveAsync(Arg.Any<IEnumerable<Guid>>(), user.Id)
            .Returns(repoRevisionDate);

        // Act
        var result = await sutProvider.Sut.UnarchiveManyAsync(new[] { cipher.Id }, user.Id);

        // Assert
        var unarchivedCipher = Assert.Single(result);
        Assert.Equal(repoRevisionDate, unarchivedCipher.RevisionDate);
        Assert.Null(unarchivedCipher.ArchivedDate);
    }
}
