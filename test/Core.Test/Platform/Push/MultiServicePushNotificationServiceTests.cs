using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Platform.Push;

public class MultiServicePushNotificationServiceTests
{
    private readonly IPushEngine _fakeEngine1;
    private readonly IPushEngine _fakeEngine2;
    private readonly ICollectionCipherRepository _collectionCipherRepository;

    private readonly MultiServicePushNotificationService _sut;

    public MultiServicePushNotificationServiceTests()
    {
        _fakeEngine1 = Substitute.For<IPushEngine>();
        _fakeEngine2 = Substitute.For<IPushEngine>();
        _collectionCipherRepository = Substitute.For<ICollectionCipherRepository>();

        _sut = new MultiServicePushNotificationService(
            [_fakeEngine1, _fakeEngine2],
            _collectionCipherRepository,
            NullLogger<MultiServicePushNotificationService>.Instance,
            new GlobalSettings(),
            new FakeTimeProvider()
        );
    }

#if DEBUG // These tests require debug code in the sut to work properly
    [Fact]
    public async Task PushAsync_CallsAllEngines()
    {
        var notification = new PushNotification<object>
        {
            Target = NotificationTarget.User,
            TargetId = Guid.NewGuid(),
            Type = PushType.AuthRequest,
            Payload = new { },
            ExcludeCurrentContext = false,
        };

        await _sut.PushAsync(notification);

        await _fakeEngine1
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<object>>(n => ReferenceEquals(n, notification)));

        await _fakeEngine2
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<object>>(n => ReferenceEquals(n, notification)));
    }

    [Fact]
    public async Task PushCipherAsync_PersonalCipher_DelegatesToEngines()
    {
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            OrganizationId = null,
            RevisionDate = DateTime.UtcNow,
        };
        var collectionIds = new[] { Guid.NewGuid() };

        await _sut.PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);

        await _fakeEngine1.Received(1).PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);
        await _fakeEngine2.Received(1).PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);
        await _collectionCipherRepository.Received(0).GetUserIdsByCollectionIdsAsync(Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task PushCipherAsync_OrgCipher_FansOutPerUser()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var collectionId = Guid.NewGuid();

        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = null,
            OrganizationId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        _collectionCipherRepository
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId1, userId2]);

        await _sut.PushCipherAsync(cipher, PushType.SyncCipherCreate, [collectionId]);

        // Repository is called with the collection IDs
        await _collectionCipherRepository
            .Received(1)
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)));
        await _collectionCipherRepository.Received(0).GetManyByOrganizationIdAsync(Arg.Any<Guid>());

        // PushAsync is called per-user on each engine (2 users × 2 engines = 4 calls)
        await _fakeEngine1
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId1 &&
                n.Payload.UserId == userId1 &&
                n.Payload.OrganizationId == cipher.OrganizationId));

        await _fakeEngine1
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncCipherCreate &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId2 &&
                n.Payload.UserId == userId2 &&
                n.Payload.OrganizationId == cipher.OrganizationId));

        await _fakeEngine2
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Target == NotificationTarget.User && n.TargetId == userId1));

        await _fakeEngine2
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Target == NotificationTarget.User && n.TargetId == userId2));

        // PushCipherAsync is NOT called directly on engines for org ciphers
        await _fakeEngine1.Received(0).PushCipherAsync(Arg.Any<Cipher>(), Arg.Any<PushType>(), Arg.Any<IEnumerable<Guid>>());
        await _fakeEngine2.Received(0).PushCipherAsync(Arg.Any<Cipher>(), Arg.Any<PushType>(), Arg.Any<IEnumerable<Guid>>());
    }

    [Fact]
    public async Task PushCipherAsync_OrgCipher_NoCollectionIds_ResolvesCollections_AndNotifies()
    {
        var userId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var cipher = new Cipher
        {
            Id = Guid.NewGuid(),
            UserId = null,
            OrganizationId = Guid.NewGuid(),
            RevisionDate = DateTime.UtcNow,
        };

        _collectionCipherRepository
            .GetCollectionIdsByCipherIdAsync(cipher.Id)
            .Returns([collectionId]);

        _collectionCipherRepository
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)))
            .Returns([userId]);

        await _sut.PushCipherAsync(cipher, PushType.SyncLoginDelete, null);

        await _collectionCipherRepository.Received(1).GetCollectionIdsByCipherIdAsync(cipher.Id);
        await _collectionCipherRepository.Received(0).GetManyByOrganizationIdAsync(Arg.Any<Guid>());
        await _collectionCipherRepository
            .Received(1)
            .GetUserIdsByCollectionIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(collectionId)));

        await _fakeEngine1
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncLoginDelete &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId &&
                n.Payload.OrganizationId == cipher.OrganizationId &&
                n.Payload.CollectionIds != null &&
                n.Payload.CollectionIds.Contains(collectionId)));

        await _fakeEngine2
            .Received(1)
            .PushAsync(Arg.Is<PushNotification<SyncCipherPushNotification>>(n =>
                n.Type == PushType.SyncLoginDelete &&
                n.Target == NotificationTarget.User &&
                n.TargetId == userId));
    }

#endif
}
