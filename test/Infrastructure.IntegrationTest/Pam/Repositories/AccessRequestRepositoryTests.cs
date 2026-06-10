using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessRequestRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_ReturnsPendingWithDenormalizedFields(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var requester = await userRepository.CreateTestUserAsync("requester");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var pending = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, AccessRequestStatus.Pending, now));
        // A resolved request on the same collection must NOT appear in the pending inbox.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, AccessRequestStatus.Denied, now));

        var pendingRows = await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([collection.Id]);

        var row = Assert.Single(pendingRows);
        Assert.Equal(pending.Id, row.Id);
        Assert.Equal(AccessRequestStatus.Pending, row.Status);
        Assert.Equal(collection.Name, row.CollectionName);
        Assert.Equal(requester.Email, row.RequesterEmail);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_OtherCollection_NotReturned(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Pending, now));

        var rows = await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([Guid.NewGuid()]);

        Assert.Empty(rows);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxHistoryByCollectionIdsAsync_RespectsStatusAndWindow(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var resolved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Approved, now));
        // Pending requests are excluded from history.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Pending, now));
        // A resolved request older than the window is excluded.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Denied, now.AddDays(-120)));

        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-90));

        var row = Assert.Single(history);
        Assert.Equal(resolved.Id, row.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_Approve_ResolvesRequestRecordsDecisionAndMintsActiveLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        // Window straddles now so the minted lease is immediately active and findable by the requester.
        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now.AddHours(-1),
            NotAfter = now.AddHours(1),
            Reason = "audit",
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
        });

        var decision = new AccessDecision
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approverId,
            Verdict = AccessDecisionVerdict.Approve,
            Comment = "approved for audit",
            CreationDate = now,
        };

        var lease = new AccessLease
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestStatus.Approved, lease, now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessRequestStatus.Approved, persisted!.Status);
        Assert.NotNull(persisted.ResolvedDate);

        // The human decision surfaces as the resolver in the inbox projection.
        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1));
        var row = Assert.Single(history);
        Assert.Equal(approverId, row.ApproverId);
        Assert.Equal("approved for audit", row.ApproverComment);

        // The approval minted an active lease spanning the request's window, so the requester now holds access.
        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(request.RequesterId, request.CipherId, now);
        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
        Assert.Equal(AccessLeaseStatus.Active, active.Status);
        Assert.Equal(request.Id, active.AccessRequestId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_Deny_ResolvesWithoutLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now.AddHours(-1),
            NotAfter = now.AddHours(1),
            Reason = "audit",
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
        });

        var decision = new AccessDecision
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        };

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestStatus.Denied, null, now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestStatus.Denied, persisted!.Status);

        // A denial grants nothing: no active lease exists for the requester.
        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(request.RequesterId, request.CipherId, now);
        Assert.Null(active);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByRequesterIdAsync_ReturnsOwnRequestsRegardlessOfStatus(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var pending = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestStatus.Pending, now));
        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestStatus.Denied, now.AddMinutes(-1)));
        // A different user's request on the same collection must not appear.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Pending, now));

        var mine = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId);

        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, r => r.Id == pending.Id);
        Assert.Contains(mine, r => r.Id == denied.Id);
        // Caller-scoped self-read omits the display-name joins.
        Assert.All(mine, r => Assert.Null(r.CollectionName));
    }

    private static AccessRequest BuildRequest(
        Guid organizationId, Guid collectionId, Guid requesterId, AccessRequestStatus status, DateTime creationDate)
        => new()
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = Guid.NewGuid(),
            RequesterId = requesterId,
            NotBefore = creationDate.AddHours(1),
            NotAfter = creationDate.AddHours(2),
            Reason = "audit",
            Status = status,
            CreationDate = creationDate,
            ResolvedDate = status == AccessRequestStatus.Pending ? null : creationDate,
        };
}
