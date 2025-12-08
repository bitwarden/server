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
public class ArchiveCiphersCommandTest
{
    [Theory]
    [BitAutoData(true, false, 1, 1, 1)]
    [BitAutoData(false, false, 1, 0, 1)]
    [BitAutoData(false, true, 1, 0, 1)]
    [BitAutoData(true, true, 1, 0, 1)]
    public async Task ArchiveManyAsync_Works(
        bool isEditable, bool hasOrganizationId,
        int cipherRepoCalls, int resultCountFromQuery, int pushNotificationsCalls,
        SutProvider<ArchiveCiphersCommand> sutProvider, CipherDetails cipher, User user)
    {
        cipher.Edit = isEditable;
        cipher.OrganizationId = hasOrganizationId ? Guid.NewGuid() : null;

        var cipherList = new List<CipherDetails> { cipher };

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id).Returns(cipherList);

        // Act
        await sutProvider.Sut.ArchiveManyAsync([cipher.Id], user.Id);

        // Assert
        await sutProvider.GetDependency<ICipherRepository>().Received(cipherRepoCalls).ArchiveAsync(
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
    public async Task ArchiveManyAsync_SetsArchivedDateOnReturnedCiphers(
        SutProvider<ArchiveCiphersCommand> sutProvider,
        CipherDetails cipher,
        User user)
    {
        // Arrange: make it archivable
        cipher.Edit = true;
        // Allow organization cipher to be archived in this test
        cipher.OrganizationId = Guid.Parse("3f2504e0-4f89-11d3-9a0c-0305e82c3301");

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<CipherDetails> { cipher });

        var repoRevisionDate = DateTime.UtcNow;

        sutProvider.GetDependency<ICipherRepository>()
            .ArchiveAsync(Arg.Any<IEnumerable<Guid>>(), user.Id)
            .Returns(repoRevisionDate);

        // Act
        var result = await sutProvider.Sut.ArchiveManyAsync(new[] { cipher.Id }, user.Id);

        // Assert
        var archivedCipher = Assert.Single(result);
        Assert.Equal(repoRevisionDate, archivedCipher.RevisionDate);
        Assert.Equal(repoRevisionDate, archivedCipher.ArchivedDate);
    }

}
