using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class LeaseRequestRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_ReturnsPendingWithDenormalizedFields(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var requester = await userRepository.CreateTestUserAsync("requester");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var pending = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, LeaseRequestStatus.Pending, now));
        // A resolved request on the same collection must NOT appear in the pending inbox.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, LeaseRequestStatus.Denied, now));

        var pendingRows = await leaseRequestRepository.GetManyInboxPendingByCollectionIdsAsync([collection.Id]);

        var row = Assert.Single(pendingRows);
        Assert.Equal(pending.Id, row.Id);
        Assert.Equal(LeaseRequestStatus.Pending, row.Status);
        Assert.Equal(collection.Name, row.CollectionName);
        Assert.Equal(requester.Email, row.RequesterEmail);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_OtherCollection_NotReturned(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));

        var rows = await leaseRequestRepository.GetManyInboxPendingByCollectionIdsAsync([Guid.NewGuid()]);

        Assert.Empty(rows);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxHistoryByCollectionIdsAsync_RespectsStatusAndWindow(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var resolved = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Approved, now));
        // Pending requests are excluded from history.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));
        // A resolved request older than the window is excluded.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Denied, now.AddDays(-120)));

        var history = await leaseRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-90));

        var row = Assert.Single(history);
        Assert.Equal(resolved.Id, row.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_Approve_ResolvesRequestRecordsDecisionAndMintsActiveLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        // Window straddles now so the minted lease is immediately active and findable by the requester.
        var request = await leaseRequestRepository.CreateAsync(new LeaseRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now.AddHours(-1),
            NotAfter = now.AddHours(1),
            Reason = "audit",
            Status = LeaseRequestStatus.Pending,
            CreationDate = now,
        });

        var decision = new LeaseDecision
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Human,
            ApproverId = approverId,
            Decision = LeaseDecisionVerdict.Approve,
            Comment = "approved for audit",
            CreationDate = now,
        };

        var lease = new Lease
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Status = LeaseStatus.Active,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };

        await leaseRequestRepository.ResolveWithDecisionAsync(request, decision, LeaseRequestStatus.Approved, lease, now);

        var persisted = await leaseRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(LeaseRequestStatus.Approved, persisted!.Status);
        Assert.NotNull(persisted.ResolvedDate);

        // The human decision surfaces as the resolver in the inbox projection.
        var history = await leaseRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1));
        var row = Assert.Single(history);
        Assert.Equal(approverId, row.ResolverId);
        Assert.Equal("approved for audit", row.ResolverComment);

        // The approval minted an active lease spanning the request's window, so the requester now holds access.
        var active = await leaseRepository.GetActiveByRequesterIdCipherIdAsync(request.RequesterId, request.CipherId, now);
        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
        Assert.Equal(LeaseStatus.Active, active.Status);
        Assert.Equal(request.Id, active.LeaseRequestId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_Deny_ResolvesWithoutLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await leaseRequestRepository.CreateAsync(new LeaseRequest
        {
            OrganizationId = organization.Id,
            CollectionId = collection.Id,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now.AddHours(-1),
            NotAfter = now.AddHours(1),
            Reason = "audit",
            Status = LeaseRequestStatus.Pending,
            CreationDate = now,
        });

        var decision = new LeaseDecision
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Human,
            ApproverId = Guid.NewGuid(),
            Decision = LeaseDecisionVerdict.Deny,
            CreationDate = now,
        };

        await leaseRequestRepository.ResolveWithDecisionAsync(request, decision, LeaseRequestStatus.Denied, null, now);

        var persisted = await leaseRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(LeaseRequestStatus.Denied, persisted!.Status);

        // A denial grants nothing: no active lease exists for the requester.
        var active = await leaseRepository.GetActiveByRequesterIdCipherIdAsync(request.RequesterId, request.CipherId, now);
        Assert.Null(active);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByRequesterIdAsync_ReturnsOwnRequestsRegardlessOfStatus(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var pending = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, LeaseRequestStatus.Pending, now));
        var denied = await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, LeaseRequestStatus.Denied, now.AddMinutes(-1)));
        // A different user's request on the same collection must not appear.
        await leaseRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), LeaseRequestStatus.Pending, now));

        var mine = await leaseRequestRepository.GetManyByRequesterIdAsync(requesterId);

        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, r => r.Id == pending.Id);
        Assert.Contains(mine, r => r.Id == denied.Id);
        // Caller-scoped self-read omits the display-name joins.
        Assert.All(mine, r => Assert.Null(r.CollectionName));
    }

    private static LeaseRequest BuildRequest(
        Guid organizationId, Guid collectionId, Guid requesterId, LeaseRequestStatus status, DateTime creationDate)
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
            ResolvedDate = status == LeaseRequestStatus.Pending ? null : creationDate,
        };
}
