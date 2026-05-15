using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
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
        cipher.UserId = Guid.NewGuid();

        await sutProvider.Sut.PushSyncCipherCreateAsync(cipher, collectionIds);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == cipher.UserId.Value &&
                n.Payload.Id == cipher.Id &&
                n.Payload.UserId == cipher.UserId &&
                n.ExcludeCurrentContext));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherUpdateAsync_PersonalCipher_PushesCorrectNotification(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = null;
        cipher.UserId = Guid.NewGuid();

        await sutProvider.Sut.PushSyncCipherUpdateAsync(cipher, []);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherUpdate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == cipher.UserId.Value &&
                n.Payload.Id == cipher.Id));
    }

    [Theory, BitAutoData]
    public async Task PushSyncCipherDeleteAsync_PersonalCipher_PushesCorrectNotification(
        SutProvider<CipherSyncPushService> sutProvider, Cipher cipher)
    {
        cipher.OrganizationId = null;
        cipher.UserId = Guid.NewGuid();

        await sutProvider.Sut.PushSyncCipherDeleteAsync(cipher);

        await sutProvider.GetDependency<IPushNotificationService>().Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncLoginDelete &&
                n.Target == NotificationTarget.User &&
                n.TargetId == cipher.UserId.Value &&
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
}
