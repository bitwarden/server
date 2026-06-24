using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
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
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Revoked, auditDecision, now);

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

        // The human decision surfaces as a single element of the inbox projection's decision log.
        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1));
        var row = Assert.Single(history);
        var recorded = Assert.Single(row.Decisions);
        Assert.Equal(AccessDeciderKind.Human, recorded.DeciderKind);
        Assert.Equal(approverId, recorded.Id!.Value);
        Assert.Equal("approved for audit", recorded.Comment);
        // Verdict and decision timestamp come straight from the AccessDecision row, so the contract exposes what each
        // approver decided and when.
        Assert.Equal(AccessDecisionVerdict.Approve, recorded.Verdict);
        Assert.Equal(now, recorded.DecidedAt);
        // The approver id here belongs to no User row, so the identity join yields null name/email and the client
        // falls back to the id. Identity resolution against a real User is covered by the My Requests read test.
        Assert.Null(recorded.Name);
        Assert.Null(recorded.Email);

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
    public async Task GetManyByRequesterIdAsync_ResolvesHumanApproverIdentity(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The requester's own list names who decided the request. The collection/cipher/requester joins stay
        // omitted (those names come from the caller's vault), but the approver identity must resolve from the
        // human decision's User so the client shows a name instead of a raw id.
        var approver = await userRepository.CreateTestUserAsync("approver");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestStatus.Pending, now));

        var decision = new AccessDecision
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approver.Id,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "not now",
            CreationDate = now,
        };
        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestStatus.Denied, now);

        var mine = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId);

        var row = Assert.Single(mine);
        var resolver = Assert.Single(row.Decisions);
        Assert.Equal(AccessDeciderKind.Human, resolver.DeciderKind);
        Assert.Equal(approver.Id, resolver.Id!.Value);
        Assert.Equal(approver.Name, resolver.Name);
        Assert.Equal(approver.Email, resolver.Email);
        Assert.Equal("not now", resolver.Comment);
        Assert.Equal(AccessDecisionVerdict.Deny, resolver.Verdict);
        Assert.Equal(now, resolver.DecidedAt);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxHistoryByCollectionIdsAsync_MultipleHumanDecisions_ProjectsFullHistoryOldestFirst(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // AccessDecision is 1-to-many with AccessRequest, so the approvers array carries every human decision oldest
        // first: an approval followed by a managing approver retracting the unactivated approval surfaces both, rather
        // than collapsing to a single resolver.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var firstApproverId = Guid.NewGuid();
        var secondApproverId = Guid.NewGuid();

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

        // First decision: approve.
        await accessRequestRepository.ResolveWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CoreHelpers.GenerateComb(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = firstApproverId,
                Verdict = AccessDecisionVerdict.Approve,
                Comment = "approved",
                CreationDate = now,
            },
            AccessRequestStatus.Approved,
            now);

        // Second decision: a managing approver retracts the still-unactivated approval (records a Deny).
        await accessRequestRepository.CancelWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CoreHelpers.GenerateComb(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = secondApproverId,
                Verdict = AccessDecisionVerdict.Deny,
                Comment = "retracted",
                CreationDate = now.AddMinutes(1),
            },
            now.AddMinutes(1));

        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1));
        var row = Assert.Single(history);
        Assert.Equal(2, row.Decisions.Count);
        Assert.Equal(AccessDeciderKind.Human, row.Decisions[0].DeciderKind);
        Assert.Equal(firstApproverId, row.Decisions[0].Id!.Value);
        Assert.Equal(AccessDecisionVerdict.Approve, row.Decisions[0].Verdict);
        Assert.Equal("approved", row.Decisions[0].Comment);
        Assert.Equal(secondApproverId, row.Decisions[1].Id!.Value);
        Assert.Equal(AccessDecisionVerdict.Deny, row.Decisions[1].Verdict);
        Assert.Equal("retracted", row.Decisions[1].Comment);
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

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_ReturnsDenormalizedFieldsAndDecisionLog(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The dedicated request page reads one request by id with the same denormalized projection the inbox reads use
        // (collection/requester names) plus the full decision log — unlike the caller-scoped "mine" read which omits the
        // collection/cipher/requester joins.
        var requester = await userRepository.CreateTestUserAsync("requester");
        var approver = await userRepository.CreateTestUserAsync("approver");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, AccessRequestStatus.Pending, now));

        await accessRequestRepository.ResolveWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CoreHelpers.GenerateComb(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = approver.Id,
                Verdict = AccessDecisionVerdict.Approve,
                Comment = "approved for audit",
                CreationDate = now,
            },
            AccessRequestStatus.Approved,
            now);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id);

        Assert.NotNull(details);
        Assert.Equal(request.Id, details!.Id);
        Assert.Equal(AccessRequestStatus.Approved, details.Status);
        // Denormalized display fields are populated (unlike the caller-scoped "mine" read).
        Assert.Equal(collection.Name, details.CollectionName);
        Assert.Equal(requester.Name, details.RequesterName);
        Assert.Equal(requester.Email, details.RequesterEmail);
        // The full decision log projects with the human approver's resolved identity.
        var decision = Assert.Single(details.Decisions);
        Assert.Equal(AccessDeciderKind.Human, decision.DeciderKind);
        Assert.Equal(approver.Id, decision.Id!.Value);
        Assert.Equal(approver.Email, decision.Email);
        Assert.Equal("approved for audit", decision.Comment);
        Assert.Equal(AccessDecisionVerdict.Approve, decision.Verdict);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_UnknownId_ReturnsNull(IAccessRequestRepository accessRequestRepository)
    {
        Assert.Null(await accessRequestRepository.GetDetailsByIdAsync(Guid.NewGuid()));
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
