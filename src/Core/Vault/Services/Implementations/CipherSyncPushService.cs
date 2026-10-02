using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Vault.Services;

public class CipherSyncPushService : ICipherSyncPushService
{
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly IFeatureService _featureService;
    private readonly ILogger<CipherSyncPushService> _logger;

    public CipherSyncPushService(
        IPushNotificationService pushNotificationService,
        ICollectionCipherRepository collectionCipherRepository,
        IFeatureService featureService,
        ILogger<CipherSyncPushService> logger)
    {
        _pushNotificationService = pushNotificationService;
        _collectionCipherRepository = collectionCipherRepository;
        _featureService = featureService;
        _logger = logger;
    }

    public Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);

    public Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);

    public Task PushSyncCipherDeleteAsync(Cipher cipher, IEnumerable<Guid>? collectionIds = null)
        => PushCipherAsync(cipher, PushType.SyncLoginDelete, collectionIds);

    private async Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            if (!_featureService.IsEnabled(FeatureFlagKeys.OrgCipherPushFanout))
            {
                // Device registrations in Notification Hub and Relay are not collection-aware, so we cannot
                // safely fan out to individual users on those mobile engines. Restrict to the non-mobile
                // (SignalR) path, which routes by organizationId on the receiving end.
                await _pushNotificationService.PushAsync(new PushNotification<SyncCipherPushNotification>
                {
                    Type = pushType,
                    Target = NotificationTarget.Organization,
                    TargetId = cipher.OrganizationId.Value,
                    Payload = new SyncCipherPushNotification
                    {
                        Id = cipher.Id,
                        OrganizationId = cipher.OrganizationId,
                        RevisionDate = cipher.RevisionDate,
                    },
                    ExcludeCurrentContext = true,
                    NonMobileOnly = true,
                });
                return;
            }

            var collectionIdList = collectionIds?.Distinct().ToList() ?? [];
            if (collectionIdList.Count == 0)
            {
                collectionIdList = [.. await _collectionCipherRepository.GetCollectionIdsByCipherIdAsync(cipher.Id)];
                if (collectionIdList.Count == 0)
                {
                    _logger.LogWarning(
                        "Skipping push notification for organization cipher {CipherId} in organization {OrganizationId} because no collection IDs were provided or found.",
                        cipher.Id,
                        cipher.OrganizationId.Value);
                    return;
                }
            }

            var userIds = await _collectionCipherRepository.GetUserIdsByCollectionIdsAsync(collectionIdList);
            var pushTasks = userIds.Select(userId =>
                _pushNotificationService.PushAsync(new PushNotification<SyncCipherPushNotification>
                {
                    Type = pushType,
                    Target = NotificationTarget.User,
                    TargetId = userId,
                    Payload = new SyncCipherPushNotification
                    {
                        Id = cipher.Id,
                        UserId = userId,
                        OrganizationId = cipher.OrganizationId,
                        RevisionDate = cipher.RevisionDate,
                        CollectionIds = collectionIdList,
                    },
                    ExcludeCurrentContext = true,
                }));

            await Task.WhenAll(pushTasks);
            return;
        }

        if (!cipher.UserId.HasValue)
        {
            return;
        }

        await _pushNotificationService.PushAsync(new PushNotification<SyncCipherPushNotification>
        {
            Type = pushType,
            Target = NotificationTarget.User,
            TargetId = cipher.UserId.Value,
            Payload = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            },
            ExcludeCurrentContext = true,
        });
    }
}
