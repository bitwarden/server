using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bitwarden.Server.Sdk.Features;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Vault.Services;

public class CipherSyncPushService : ICipherSyncPushService
{
    /// <summary>
    /// Above this many ciphers, one <see cref="PushType.SyncCiphers"/> costs less than a push per cipher.
    /// </summary>
    public const int MaxSyncCipherUserFieldsPushes = 10;

    private readonly IPushNotificationService _pushNotificationService;
    private readonly ICollectionCipherRepository _collectionCipherRepository;
    private readonly ICipherRepository _cipherRepository;
    private readonly IFeatureService _featureService;
    private readonly ILogger<CipherSyncPushService> _logger;

    public CipherSyncPushService(
        IPushNotificationService pushNotificationService,
        ICollectionCipherRepository collectionCipherRepository,
        ICipherRepository cipherRepository,
        IFeatureService featureService,
        ILogger<CipherSyncPushService> logger)
    {
        _pushNotificationService = pushNotificationService;
        _collectionCipherRepository = collectionCipherRepository;
        _cipherRepository = cipherRepository;
        _featureService = featureService;
        _logger = logger;
    }

    public Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);

    public Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
        => PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);

    public Task PushSyncCipherDeleteAsync(Cipher cipher, IEnumerable<Guid>? collectionIds = null)
        => PushCipherAsync(cipher, PushType.SyncLoginDelete, collectionIds);

    public async Task PushSyncCipherUserFieldsAsync(Guid userId, IReadOnlyCollection<CipherDetails> ciphers,
        bool excludeCurrentContextFromSyncCiphers)
    {
        if (!ShouldPushUserFields(ciphers.Count))
        {
            await _pushNotificationService.PushSyncCiphersAsync(userId, excludeCurrentContextFromSyncCiphers);
            return;
        }

        await PushUserFieldsAsync(userId, ciphers);
    }

    public async Task PushSyncCipherUserFieldsAsync(Guid userId, IReadOnlyCollection<Guid> cipherIds,
        bool excludeCurrentContextFromSyncCiphers)
    {
        if (!ShouldPushUserFields(cipherIds.Count))
        {
            await _pushNotificationService.PushSyncCiphersAsync(userId, excludeCurrentContextFromSyncCiphers);
            return;
        }

        var ciphers = new List<CipherDetails>();
        foreach (var cipherId in cipherIds.Distinct())
        {
            // Scoped to the user, so a cipher the user cannot see is skipped.
            var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);
            if (cipher is not null)
            {
                ciphers.Add(cipher);
            }
        }

        await PushUserFieldsAsync(userId, ciphers);
    }

    private bool ShouldPushUserFields(int cipherCount)
        => cipherCount <= MaxSyncCipherUserFieldsPushes
           && _featureService.IsEnabled(FeatureFlagKeys.PM46336_SyncCipherUserFieldsPush);

    private Task PushUserFieldsAsync(Guid userId, IEnumerable<CipherDetails> ciphers)
        => Task.WhenAll(ciphers.Select(cipher =>
            _pushNotificationService.PushAsync(new PushNotification<SyncCipherUserFieldsPushNotification>
            {
                Type = PushType.SyncCipherUserFields,
                Target = NotificationTarget.User,
                TargetId = userId,
                Payload = new SyncCipherUserFieldsPushNotification
                {
                    Id = cipher.Id,
                    UserId = userId,
                    FolderId = cipher.FolderId,
                    Favorite = cipher.Favorite,
                    ArchivedDate = cipher.ArchivedDate,
                },
                ExcludeCurrentContext = true,
            })));

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
