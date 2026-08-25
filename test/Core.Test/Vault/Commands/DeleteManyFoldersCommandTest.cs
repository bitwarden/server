using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Commands;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Commands;

[SutProviderCustomize]
public class DeleteManyFoldersCommandTest
{
    [Theory, BitAutoData]
    public async Task DeleteManyAsync_DeletesFolders_AndPushesASingleVaultSync(
        SutProvider<DeleteManyFoldersCommand> sutProvider, User user, Folder firstFolder, Folder secondFolder)
    {
        firstFolder.UserId = user.Id;
        secondFolder.UserId = user.Id;

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Folder> { firstFolder, secondFolder });

        await sutProvider.Sut.DeleteManyAsync([firstFolder.Id, secondFolder.Id], user.Id);

        await sutProvider.GetDependency<IFolderRepository>().Received(1).DeleteManyAsync(
            Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 2 && ids.Contains(firstFolder.Id) && ids.Contains(secondFolder.Id)),
            user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(p =>
                p.Type == PushType.SyncVault && p.TargetId == user.Id));
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushAsync(Arg.Is<PushNotification<SyncFolderPushNotification>>(p =>
                p.Type == PushType.SyncFolderDelete));
    }

    [Theory, BitAutoData]
    public async Task DeleteManyAsync_IgnoresIdsTheUserDoesNotOwn(
        SutProvider<DeleteManyFoldersCommand> sutProvider, User user, Folder folder)
    {
        folder.UserId = user.Id;
        var otherUsersFolderId = Guid.NewGuid();

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Folder> { folder });

        await sutProvider.Sut.DeleteManyAsync([folder.Id, otherUsersFolderId], user.Id);

        await sutProvider.GetDependency<IFolderRepository>().Received(1).DeleteManyAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == folder.Id),
            user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(p =>
                p.Type == PushType.SyncVault && p.TargetId == user.Id));
    }

    [Theory, BitAutoData]
    public async Task DeleteManyAsync_DeduplicatesRepeatedIds(
        SutProvider<DeleteManyFoldersCommand> sutProvider, User user, Folder folder)
    {
        folder.UserId = user.Id;

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Folder> { folder });

        await sutProvider.Sut.DeleteManyAsync([folder.Id, folder.Id], user.Id);

        await sutProvider.GetDependency<IFolderRepository>().Received(1).DeleteManyAsync(
            Arg.Is<IEnumerable<Guid>>(ids => ids.Single() == folder.Id),
            user.Id);
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(p =>
                p.Type == PushType.SyncVault && p.TargetId == user.Id));
    }

    [Theory, BitAutoData]
    public async Task DeleteManyAsync_WhenNoIdsMatch_DoesNothing(
        SutProvider<DeleteManyFoldersCommand> sutProvider, User user, Folder folder)
    {
        folder.UserId = user.Id;

        sutProvider.GetDependency<IFolderRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns(new List<Folder> { folder });

        await sutProvider.Sut.DeleteManyAsync([Guid.NewGuid()], user.Id);

        await sutProvider.GetDependency<IFolderRepository>().DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default, default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceive()
            .PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task DeleteManyAsync_WithNoIds_ThrowsBadRequest(
        SutProvider<DeleteManyFoldersCommand> sutProvider, User user)
    {
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.DeleteManyAsync([], user.Id));

        Assert.Equal("No folder ids provided.", exception.Message);
        await sutProvider.GetDependency<IFolderRepository>().DidNotReceiveWithAnyArgs()
            .DeleteManyAsync(default, default);
    }
}
