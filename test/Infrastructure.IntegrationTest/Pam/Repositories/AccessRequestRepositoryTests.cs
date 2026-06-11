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
    public async Task GetManyInboxHistoryByCollectionIdsAsync_SurfacesProducedLeaseStatus(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var approved = BuildRequest(organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Approved, now);
        approved.NotBefore = now.AddHours(-1);
        approved.NotAfter = now.AddHours(1);
        approved = await accessRequestRepository.CreateAsync(approved);

        var lease = new AccessLease
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = approved.Id,
            OrganizationId = approved.OrganizationId,
            CollectionId = approved.CollectionId,
            CipherId = approved.CipherId,
            RequesterId = approved.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = approved.NotBefore,
            NotAfter = approved.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        // While the lease is active the inbox sees its Active status, so the client offers Revoke.
        var active = Assert.Single(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1)));
        Assert.Equal(lease.Id, active.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, active.ProducedLeaseStatus);

        // After the lease ends the inbox sees the Revoked status (the window is unchanged), so the client can keep
        // the row out of the Active group and stop offering a Revoke that the server would now reject.
        var auditDecision = new AccessDecision
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = approved.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        };
        await accessLeaseRepository.RevokeAsync(lease, auditDecision, now);

        var revoked = Assert.Single(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1)));
        Assert.Equal(lease.Id, revoked.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Revoked, revoked.ProducedLeaseStatus);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_Approve_ResolvesRequestAndRecordsDecisionWithoutMintingLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

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

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestStatus.Approved, now);

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

        // Approval records the verdict only: no lease exists until the requester activates the approved request,
        // so the requester does not yet hold access and the inbox row carries no produced lease.
        Assert.Null(row.ProducedLeaseId);
        Assert.Null(row.ProducedLeaseStatus);
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
        Assert.Null(await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(
            request.RequesterId, request.CipherId, now));

        // The approved request is now the requester's startable approval for this cipher.
        var approved = await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            request.RequesterId, request.CipherId, now);
        Assert.NotNull(approved);
        Assert.Equal(request.Id, approved!.Id);
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

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestStatus.Denied, now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestStatus.Denied, persisted!.Status);

        // A denial grants nothing: no active lease exists for the requester.
        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(request.RequesterId, request.CipherId, now);
        Assert.Null(active);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveApprovedByRequesterIdCipherIdAsync_ReturnsStartableApprovalsOnly(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        // Approved with an open window: the startable approval the query must return.
        var openWindow = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestStatus.Approved, now);
        openWindow.NotBefore = now.AddHours(-1);
        openWindow.NotAfter = now.AddHours(1);
        var startable = await accessRequestRepository.CreateAsync(openWindow);

        var found = await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, startable.CipherId, now);
        Assert.NotNull(found);
        Assert.Equal(startable.Id, found!.Id);

        // Approved with a future window is included — the client shows the upcoming window.
        var future = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestStatus.Approved, now));
        Assert.NotNull(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, future.CipherId, now));

        // Approved with a lapsed window is excluded — it can never be activated.
        var lapsed = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestStatus.Approved, now);
        lapsed.NotBefore = now.AddHours(-2);
        lapsed.NotAfter = now.AddHours(-1);
        lapsed = await accessRequestRepository.CreateAsync(lapsed);
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, lapsed.CipherId, now));

        // Pending and denied requests are not approvals.
        var pending = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestStatus.Pending, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, pending.CipherId, now));
        var denied = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestStatus.Denied, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, denied.CipherId, now));

        // Another user's approval for the same cipher is not the caller's.
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            Guid.NewGuid(), startable.CipherId, now));

        // Once the approval produces a lease it is activated, not approved, and leaves this read.
        var lease = new AccessLease
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = startable.Id,
            OrganizationId = startable.OrganizationId,
            CollectionId = startable.CollectionId,
            CipherId = startable.CipherId,
            RequesterId = startable.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = startable.NotBefore,
            NotAfter = startable.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, startable.CipherId, now));
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

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_PendingRequest_TransitionsToCancelledAndStampsResolvedDate(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Pending, now));

        var resolvedAt = now.AddMinutes(5);
        await accessRequestRepository.CancelAsync(request.Id, resolvedAt);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessRequestStatus.Cancelled, persisted!.Status);
        Assert.NotNull(persisted.ResolvedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_AlreadyResolvedRequest_LeavesItUntouched(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // The proc only acts on Pending rows, so a request that already left Pending (e.g. approved) is never
        // clobbered into Cancelled by a stray/raced cancel.
        var approved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestStatus.Approved, now));

        await accessRequestRepository.CancelAsync(approved.Id, now.AddMinutes(5));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestStatus.Approved, persisted!.Status);
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
