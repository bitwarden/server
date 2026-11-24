using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bitwarden.Server.Sdk.Features;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Vault.Services;

[SutProviderCustomize]
public class CipherSyncPushServiceTests
{
    [Theory, BitAutoData]
    public async Task PushSyncCipherCreateAsync_PersonalCipher_PushesCorrectNotification(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, List<Guid> collectionIds)
    {
        cipher.OrganizationId = null;
        var userId = Guid.NewGuid();
        cipher.UserId = userId;

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, collectionIds);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.Id == cipher.Id &&
                n.Payload.UserId == cipher.UserId &&
                n.ExcludeCurrentContext));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUpdateAsync_PersonalCipher_PushesCorrectNotification(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = null;
        var userId = Guid.NewGuid();
        cipher.UserId = userId;

        await sutProvider.Sut.PushSyncCipherUpdateAsync(cipher, []);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherUpdate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.Id == cipher.Id));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherDeleteAsync_PersonalCipher_PushesCorrectNotification(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = null;
        cipher.UserId = Guid.NewGuid();
        var userId = cipher.UserId.Value;

        await sutProvider.Sut.PushSyncCipherDeleteAsync(cipher);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncLoginDelete &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.Id == cipher.Id));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherCreateAsync_PersonalCipher_NoUserId_NoPush(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = null;
        cipher.UserId = null;

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, []);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<SyncCipherPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherCreateAsync_OrgCipher_FlagOff_SendsNonMobileOrgBroadcast(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, Guid collectionId)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(false);

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, [collectionId]);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.Organization &&
                n.TargetId == cipher.OrganizationId!.Value &&
                n.NonMobileOnly == true &&
                n.Payload.Id == cipher.Id &&
                n.Payload.OrganizationId == cipher.OrganizationId));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherCreateAsync_OrgCipher_FlagOn_FansOutPerUser(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, Guid collectionId, Guid userId1, Guid userId2)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(true);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId1, userId2]);

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, [collectionId]);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId1 &&
                n.Payload.UserId == userId1 &&
                n.Payload.OrganizationId == cipher.OrganizationId &&
                n.ExcludeCurrentContext));

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId2 &&
                n.Payload.UserId == userId2 &&
                n.Payload.OrganizationId == cipher.OrganizationId));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherDeleteAsync_OrgCipher_FlagOn_NoCollectionIds_ResolvesFromRepo(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, Guid collectionId, Guid userId)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(true);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetCollectionIdsByCipherIdAsync(cipher.Id)
            .Returns([collectionId]);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId]);

        await sutProvider.Sut.PushSyncCipherDeleteAsync(cipher);

        await sutProvider.GetDependency<ICollectionCipherRepository>()
            .Received(1)
            .GetCollectionIdsByCipherIdAsync(cipher.Id);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncLoginDelete &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.OrganizationId == cipher.OrganizationId &&
                n.Payload.CollectionIds != null &&
                n.Payload.CollectionIds.Contains(collectionId)));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherCreateAsync_OrgCipher_FlagOn_NoCollectionIdsFound_LogsWarning_NoPush(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(true);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetCollectionIdsByCipherIdAsync(cipher.Id)
            .Returns([]);

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, []);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<SyncCipherPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUpdateAsync_OrgCipher_FlagOn_FansOutPerUser(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, Guid collectionId, Guid userId1, Guid userId2)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(true);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId1, userId2]);

        await sutProvider.Sut.PushSyncCipherUpdateAsync(cipher, [collectionId]);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherUpdate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId1 &&
                n.Payload.UserId == userId1 &&
                n.Payload.OrganizationId == cipher.OrganizationId &&
                n.ExcludeCurrentContext));

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherUpdate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId2 &&
                n.Payload.UserId == userId2 &&
                n.Payload.OrganizationId == cipher.OrganizationId));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUpdateAsync_OrgCipher_FlagOn_EmptyCollectionIds_FallsBackToRepo(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher, Guid collectionId, Guid userId)
    {
        cipher.OrganizationId = Guid.NewGuid();
        cipher.UserId = null;

        sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.OrgCipherPushFanout)
            .Returns(true);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetCollectionIdsByCipherIdAsync(cipher.Id)
            .Returns([collectionId]);

        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId]);

        // Simulates SoftDeleteAsync / RestoreAsync which pass Array.Empty<Guid>()
        await sutProvider.Sut.PushSyncCipherUpdateAsync(cipher, Array.Empty<Guid>());

        await sutProvider.GetDependency<ICollectionCipherRepository>()
            .Received(1)
            .GetCollectionIdsByCipherIdAsync(cipher.Id);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherUpdate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.OrganizationId == cipher.OrganizationId &&
                n.Payload.CollectionIds != null &&
                n.Payload.CollectionIds.Contains(collectionId)));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUserFieldsAsync_FlagOff_PushesSyncCiphers(
        SutProvider<CipherSyncPushService> sutProvider, Guid userId, CipherDetails cipher)
    {
        await sutProvider.Sut.PushSyncCipherUserFieldsAsync(userId, [cipher], true);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n =>
                n.Type == PushType.SyncCiphers &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.ExcludeCurrentContext));
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<SyncCipherUserFieldsPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUserFieldsAsync_FlagOn_PushesUserFieldsPerCipher(
        SutProvider<CipherSyncPushService> sutProvider, Guid userId, CipherDetails cipher1, CipherDetails cipher2)
    {
        EnableUserFieldsPush(sutProvider);
        cipher1.ArchivedDate = DateTime.UtcNow;
        cipher2.ArchivedDate = null;

        await sutProvider.Sut.PushSyncCipherUserFieldsAsync(userId, [cipher1, cipher2], true);

        foreach (var cipher in new[] { cipher1, cipher2 })
        {
            await sutProvider.GetDependency<IPushNotificationService>().Received(1)
                .PushAsync(Arg.Is<PushNotification<SyncCipherUserFieldsPushNotification>>(n =>
                    n.Type == PushType.SyncCipherUserFields &&
                    n.Target == NotificationTarget.User &&
                    n.TargetId == userId &&
                    n.ExcludeCurrentContext &&
                    n.Payload.Id == cipher.Id &&
                    n.Payload.UserId == userId &&
                    n.Payload.FolderId == cipher.FolderId &&
                    n.Payload.Favorite == cipher.Favorite &&
                    n.Payload.ArchivedDate == cipher.ArchivedDate));
        }
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUserFieldsAsync_FlagOn_TooManyCiphers_PushesSyncCiphers(
        SutProvider<CipherSyncPushService> sutProvider, Guid userId)
    {
        EnableUserFieldsPush(sutProvider);
        var ciphers = Enumerable.Range(0, CipherSyncPushService.MaxSyncCipherUserFieldsPushes + 1)
            .Select(_ => new CipherDetails { Id = Guid.NewGuid() })
            .ToList();

        await sutProvider.Sut.PushSyncCipherUserFieldsAsync(userId, ciphers, true);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n =>
                n.Type == PushType.SyncCiphers && n.TargetId == userId));
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<SyncCipherUserFieldsPushNotification>>());
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUserFieldsAsync_CipherIds_FlagOff_PushesSyncCiphersWithoutLoadingCiphers(
        SutProvider<CipherSyncPushService> sutProvider, Guid userId, Guid cipherId)
    {
        await sutProvider.Sut.PushSyncCipherUserFieldsAsync(userId, [cipherId], false);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<UserPushNotification>>(n =>
                n.Type == PushType.SyncCiphers &&
                n.TargetId == userId &&
                !n.ExcludeCurrentContext));
        await sutProvider.GetDependency<ICipherRepository>().DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUserFieldsAsync_CipherIds_FlagOn_PushesLoadedCiphersAndSkipsMissing(
        SutProvider<CipherSyncPushService> sutProvider, Guid userId, CipherDetails cipher, Guid missingCipherId)
    {
        EnableUserFieldsPush(sutProvider);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id, userId).Returns(cipher);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(missingCipherId, userId)
            .Returns((CipherDetails)null!);

        await sutProvider.Sut.PushSyncCipherUserFieldsAsync(userId, [cipher.Id, missingCipherId], false);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Any<PushNotification<SyncCipherUserFieldsPushNotification>>());
        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherUserFieldsPushNotification>>(n =>
                n.Payload.Id == cipher.Id &&
                n.Payload.FolderId == cipher.FolderId &&
                n.ExcludeCurrentContext));
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs()
            .PushAsync(Arg.Any<PushNotification<UserPushNotification>>());
    }

    private static void EnableUserFieldsPush(SutProvider<CipherSyncPushService> sutProvider)
        => sutProvider.GetDependency<IFeatureService>()
            .IsEnabled(FeatureFlagKeys.PM46336_SyncCipherUserFieldsPush)
            .Returns(true);
}
