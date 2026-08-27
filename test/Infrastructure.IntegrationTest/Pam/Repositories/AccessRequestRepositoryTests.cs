using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
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
            organization.Id, collection.Id, requester.Id, AccessRequestAction.None, now));
        // A resolved request on the same collection must NOT appear in the pending inbox.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, AccessRequestAction.Denied, now));

        var pendingRows = await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([collection.Id], now);

        var row = Assert.Single(pendingRows);
        Assert.Equal(pending.Id, row.Id);
        Assert.Equal(AccessRequestStatus.Pending, row.Status);
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
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        var rows = await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([Guid.NewGuid()], now);

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
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now));
        // Pending requests are excluded from history.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));
        // A resolved request older than the window is excluded.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now.AddDays(-120)));

        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-90), now);

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

        var approved = BuildRequest(organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now);
        approved.NotBefore = now.AddHours(-1);
        approved.NotAfter = now.AddHours(1);
        approved = await accessRequestRepository.CreateAsync(approved);

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = approved.Id,
            OrganizationId = approved.OrganizationId,
            CollectionId = approved.CollectionId,
            CipherId = approved.CipherId,
            RequesterId = approved.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = approved.NotBefore,
            NotAfter = approved.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        // While the lease is active the inbox sees its Active status, so the client offers Revoke.
        var active = Assert.Single(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1), now));
        Assert.Equal(lease.Id, active.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, active.ProducedLeaseStatus);

        // After the lease ends the inbox sees the Revoked status (the window is unchanged), so the client can keep
        // the row out of the Active group and stop offering a Revoke that the server would now reject.
        var auditDecision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = approved.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        };
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, auditDecision, now);

        var revoked = Assert.Single(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1), now));
        Assert.Equal(lease.Id, revoked.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Revoked, revoked.ProducedLeaseStatus);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ProducedLeaseStatus_LapsedLease_IsProjectedExpiredOnEveryRead(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Expiry is never stored: a lease whose window closed keeps Action None forever, and only a projection
        // against the read clock can call it Expired (PM-42355). All three projections derive it that way.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Activated three hours ago on a window that closed two hours ago.
        var (request, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-3));

        // The premise: the stored row is untouched -- no sweeper ran, and no early end was recorded.
        var stored = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, stored!.Action);
        Assert.Null(stored.RevokedDate);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(lease.Id, details!.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Expired, details.ProducedLeaseStatus);

        var mine = Assert.Single(await accessRequestRepository.GetManyByRequesterIdAsync(
            request.RequesterId, now.AddDays(-1), now));
        Assert.Equal(AccessLeaseStatus.Expired, mine.ProducedLeaseStatus);

        var history = Assert.Single(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1), now));
        Assert.Equal(AccessLeaseStatus.Expired, history.ProducedLeaseStatus);

        // Read as of a moment inside the window the same row is Active: this is a projection, not a write.
        var whileLive = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now.AddHours(-3));
        Assert.Equal(AccessLeaseStatus.Active, whileLive!.ProducedLeaseStatus);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ProducedLeaseStatus_ExtendedLease_StaysActiveEvenThoughTheRequestsWindowHasLapsed(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // An extension pushes the parent lease's NotAfter out in place and leaves the original request row behind,
        // so the request's own window is no longer the lease's. The projection must read the lease's NotAfter: off
        // the request's it would report a live, extended lease as expired.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Window [now-3h, now-1h], activated at now-2h.
        var (request, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-2));

        // Extended while still live, by two hours -- the lease now ends an hour from now.
        var extendedAt = now.AddMinutes(-90);
        var extension = BuildRequest(
            organization.Id, collection.Id, request.RequesterId, AccessRequestAction.Approved, extendedAt);
        extension.Id = CombGuid.Generate();
        extension.ExtensionOfLeaseId = lease.Id;
        extension.CipherId = request.CipherId;
        extension.NotBefore = lease.NotAfter;
        extension.NotAfter = lease.NotAfter.AddHours(2);
        extension.ActionDate = extendedAt;
        Assert.Equal(AccessLeaseExtendOutcome.Extended, await accessRequestRepository.CreateApprovedExtensionAsync(
            extension,
            new AccessDecision
            {
                Id = CombGuid.Generate(),
                AccessRequestId = extension.Id,
                DeciderKind = AccessDeciderKind.Automatic,
                Verdict = AccessDecisionVerdict.Approve,
                CreationDate = extendedAt,
            },
            extendedAt,
            denialComment: null));

        // The original request's window closed an hour ago...
        Assert.True(request.NotAfter < now);
        // ...but the lease it produced now runs an hour into the future.
        Assert.True((await accessLeaseRepository.GetByIdAsync(lease.Id))!.NotAfter > now);

        // So the original request must still report a live lease.
        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(lease.Id, details!.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, details.ProducedLeaseStatus);
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
            CreationDate = now,
        });

        var decision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approverId,
            Verdict = AccessDecisionVerdict.Approve,
            Comment = "approved for audit",
            CreationDate = now,
        };

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestAction.Approved, now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        Assert.NotNull(persisted.ActionDate);

        // The human decision surfaces as a single element of the inbox projection's decision log.
        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1), now);
        var row = Assert.Single(history);
        var recorded = Assert.Single(row.Decisions);
        Assert.Equal(AccessDeciderKind.Human, recorded.DeciderKind);
        Assert.Equal(approverId, recorded.ApproverId!.Value);
        Assert.Equal("approved for audit", recorded.Comment);
        // Verdict and decision timestamp come straight from the AccessDecision row, so the contract exposes what each
        // approver decided and when.
        Assert.Equal(AccessDecisionVerdict.Approve, recorded.Verdict);
        // Timestamps round-trip within a couple of milliseconds rather than exactly: Dapper binds DateTime as
        // DbType.DateTime (3.33 ms) on the MSSQL path, and the EF providers store microseconds.
        Assert.Equal(now, recorded.DecidedAt, LaxDateTimeComparer.Default);
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
            CreationDate = now,
        });

        var decision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        };

        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestAction.Denied, now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Denied, persisted!.Action);

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
        var openWindow = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now);
        openWindow.NotBefore = now.AddHours(-1);
        openWindow.NotAfter = now.AddHours(1);
        var startable = await accessRequestRepository.CreateAsync(openWindow);

        var found = await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, startable.CipherId, now);
        Assert.NotNull(found);
        Assert.Equal(startable.Id, found!.Id);

        // Approved with a future window is included — the client shows the upcoming window.
        var future = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now));
        Assert.NotNull(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, future.CipherId, now));

        // Approved with a lapsed window is excluded — it can never be activated.
        var lapsed = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now);
        lapsed.NotBefore = now.AddHours(-2);
        lapsed.NotAfter = now.AddHours(-1);
        lapsed = await accessRequestRepository.CreateAsync(lapsed);
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, lapsed.CipherId, now));

        // Pending and denied requests are not approvals.
        var pending = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, pending.CipherId, now));
        var denied = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, denied.CipherId, now));

        // Another user's approval for the same cipher is not the caller's.
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            Guid.NewGuid(), startable.CipherId, now));

        // Once the approval produces a lease it is activated, not approved, and leaves this read.
        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = startable.Id,
            OrganizationId = startable.OrganizationId,
            CollectionId = startable.CollectionId,
            CipherId = startable.CipherId,
            RequesterId = startable.RequesterId,
            Action = AccessLeaseAction.None,
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
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));
        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now.AddMinutes(-1)));
        // A different user's request on the same collection must not appear.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        var mine = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, now.AddDays(-1), now);

        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, r => r.Id == pending.Id);
        Assert.Contains(mine, r => r.Id == denied.Id);
        // Caller-scoped self-read omits the display-name joins.
        Assert.All(mine, r => Assert.Null(r.RequesterName));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByRequesterIdAsync_WindowsResolvedHistoryButKeepsEveryLiveRow(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The requester's own list had no retention window while the approver-side history had a 90-day one, so the
        // same resolved request outlived itself for the member who raised it and vanished for the approvers who
        // decided it (PM-42614). It now takes the same window -- but only over rows that are actually history.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var since = now.AddDays(-90);
        var requesterId = Guid.NewGuid();

        var recentlyDenied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now.AddDays(-2)));
        var longAgoDenied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now.AddDays(-120)));

        // An open request whose window can still be answered is live, whatever its age: windowing it away would drop
        // a live request out of the caller's own list, not age out its history.
        var longOpenStillAnswerable = BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now.AddDays(-120));
        longOpenStillAnswerable.NotBefore = now.AddHours(-1);
        longOpenStillAnswerable.NotAfter = now.AddHours(1);
        longOpenStillAnswerable = await accessRequestRepository.CreateAsync(longOpenStillAnswerable);

        // An unanswered request whose window has lapsed is derived Expired: history like any resolved row, so it
        // ages out with the rest. (Nothing is stored -- the clock already closed it everywhere it is read.)
        var longLapsedUnanswered = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now.AddDays(-120)));

        // An approved request whose window is still open is still activatable, whatever its age.
        var stillActivatable = BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now.AddDays(-120));
        stillActivatable.NotBefore = now.AddHours(-1);
        stillActivatable.NotAfter = now.AddHours(1);
        stillActivatable = await accessRequestRepository.CreateAsync(stillActivatable);

        // An approved request that was never activated and whose window has since closed is history like any other.
        var lapsedApproved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now.AddDays(-120)));

        var windowed = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, since, now);

        Assert.Contains(windowed, r => r.Id == recentlyDenied.Id);
        Assert.Contains(windowed, r => r.Id == longOpenStillAnswerable.Id);
        Assert.Contains(windowed, r => r.Id == stillActivatable.Id);
        Assert.DoesNotContain(windowed, r => r.Id == longAgoDenied.Id);
        Assert.DoesNotContain(windowed, r => r.Id == longLapsedUnanswered.Id);
        Assert.DoesNotContain(windowed, r => r.Id == lapsedApproved.Id);

        // A null window is "no window", which is what a server predating the parameter sends -- every row comes back.
        var unwindowed = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, null, now);

        Assert.Equal(6, unwindowed.Count);
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
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));

        var decision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approver.Id,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "not now",
            CreationDate = now,
        };
        await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestAction.Denied, now);

        var mine = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, now.AddDays(-1), now);

        var row = Assert.Single(mine);
        var resolver = Assert.Single(row.Decisions);
        Assert.Equal(AccessDeciderKind.Human, resolver.DeciderKind);
        Assert.Equal(approver.Id, resolver.ApproverId!.Value);
        Assert.Equal(approver.Name, resolver.Name);
        Assert.Equal(approver.Email, resolver.Email);
        Assert.Equal("not now", resolver.Comment);
        Assert.Equal(AccessDecisionVerdict.Deny, resolver.Verdict);
        Assert.Equal(now, resolver.DecidedAt, LaxDateTimeComparer.Default);
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
            CreationDate = now,
        });

        // First decision: approve.
        await accessRequestRepository.ResolveWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CombGuid.Generate(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = firstApproverId,
                Verdict = AccessDecisionVerdict.Approve,
                Comment = "approved",
                CreationDate = now,
            },
            AccessRequestAction.Approved,
            now);

        // Second decision: a managing approver retracts the still-unactivated approval (records a Deny).
        await accessRequestRepository.CancelWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CombGuid.Generate(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = secondApproverId,
                Verdict = AccessDecisionVerdict.Deny,
                Comment = "retracted",
                CreationDate = now.AddMinutes(1),
            },
            now.AddMinutes(1));

        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-1), now);
        var row = Assert.Single(history);
        Assert.Equal(2, row.Decisions.Count);
        Assert.Equal(AccessDeciderKind.Human, row.Decisions[0].DeciderKind);
        Assert.Equal(firstApproverId, row.Decisions[0].ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Approve, row.Decisions[0].Verdict);
        Assert.Equal("approved", row.Decisions[0].Comment);
        Assert.Equal(secondApproverId, row.Decisions[1].ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Deny, row.Decisions[1].Verdict);
        Assert.Equal("retracted", row.Decisions[1].Comment);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_PendingRequest_RecordsCancelledAndStampsActionDate(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        var resolvedAt = now.AddMinutes(5);
        await accessRequestRepository.CancelAsync(request.Id, resolvedAt);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessRequestAction.Cancelled, persisted!.Action);
        Assert.NotNull(persisted.ActionDate);
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

        // A request that has left the cancellable set entirely (denied) is never clobbered into Cancelled by a
        // stray/raced cancel. An Approved request is still cancellable until it is activated, so it is not the
        // example to use here.
        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now));

        await accessRequestRepository.CancelAsync(denied.Id, now.AddMinutes(5));

        var persisted = await accessRequestRepository.GetByIdAsync(denied.Id);
        Assert.Equal(AccessRequestAction.Denied, persisted!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_ApprovedUnactivatedRequest_TransitionsToCancelled(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The cancellable set is broader than Pending: the requester may also withdraw an approval they have not yet
        // activated, so no lease is ever minted from it.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var approved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now));

        await accessRequestRepository.CancelAsync(approved.Id, now.AddMinutes(5));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestAction.Cancelled, persisted!.Action);
        Assert.NotNull(persisted.ActionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_ActivatedRequest_LeavesItUntouched(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Once a request has produced a lease the access it granted is governed by that lease, which must be revoked
        // instead. Cancelling the request would strand an active lease behind a resolved-as-cancelled request.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var (approved, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        await accessRequestRepository.CancelAsync(approved.Id, now.AddMinutes(5));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        // The lease is untouched and still grants access.
        var persistedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, persistedLease!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_LapsedUnansweredRow_LeavesInboxAndIsHistoryAsExpired(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The clock predicate under test: nothing ever writes Expired, so only the read's @Now comparison keeps a
        // lapsed unanswered row out of the actionable inbox and hands it to the history read as derived Expired.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var open = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));
        // Created three hours ago, so its window (creation +1h .. +2h) lapsed an hour ago, still unanswered.
        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));

        var pendingRows = await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([collection.Id], now);

        var pendingRow = Assert.Single(pendingRows);
        Assert.Equal(open.Id, pendingRow.Id);

        var history = await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [collection.Id], now.AddDays(-90), now);

        var historyRow = Assert.Single(history);
        Assert.Equal(lapsed.Id, historyRow.Id);
        Assert.Equal(AccessRequestStatus.Expired, historyRow.Status);
        Assert.Null(historyRow.ResolvedDate); // nobody acted; the end time is NotAfter, not a resolution
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_LapsedUnanswered_ReturnsNull(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // A lapsed unanswered request is derived Expired: it must not block a fresh submission (the duplicate guard
        // reads through this) or prop up a dead pending banner. Only the read's @Now comparison delivers that.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));

        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            lapsed.RequesterId, lapsed.CipherId, now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_LapsedWindow_LeavesItUntouched(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // A row users already saw as derived Expired must never restamp to Cancelled: the write's own @Now guard is
        // the race-safe authority, whatever the command-level check concluded from its earlier read.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));

        await accessRequestRepository.CancelAsync(lapsed.Id, now);

        var persisted = await accessRequestRepository.GetByIdAsync(lapsed.Id);
        Assert.Equal(AccessRequestAction.None, persisted!.Action);
        Assert.Null(persisted.ActionDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelWithDecisionAsync_LapsedWindow_LeavesItUntouchedAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The manager-retraction write carries the same @Now guard: a lapsed approved-unactivated row is derived
        // Expired and must not restamp to Denied, and the retraction's verdict must not be orphaned onto it.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now.AddHours(-3)));

        await accessRequestRepository.CancelWithDecisionAsync(
            lapsed, BuildHumanDecision(lapsed.Id, Guid.NewGuid(), AccessDecisionVerdict.Deny, "too late", now), now);

        var persisted = await accessRequestRepository.GetByIdAsync(lapsed.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);

        var details = await accessRequestRepository.GetDetailsByIdAsync(lapsed.Id, now);
        Assert.Equal(AccessRequestStatus.Expired, details!.Status);
        Assert.Empty(details.Decisions);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_AlreadyResolvedRequest_LeavesItUntouchedAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // Two approvers racing on the same pending request: the first wins, and the loser's verdict is never appended.
        // Recording it anyway would leave the decision log contradicting the request's status — a Deny filed against a
        // request that reads as Approved, with no way to tell which one took effect.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        await accessRequestRepository.ResolveWithDecisionAsync(
            request, BuildHumanDecision(request.Id, winnerId, AccessDecisionVerdict.Approve, "approved", now),
            AccessRequestAction.Approved, now);

        // The losing approver's write finds the request already resolved.
        await accessRequestRepository.ResolveWithDecisionAsync(
            request, BuildHumanDecision(request.Id, loserId, AccessDecisionVerdict.Deny, "denied", now.AddMinutes(1)),
            AccessRequestAction.Denied, now.AddMinutes(1));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        var recorded = Assert.Single(details!.Decisions);
        Assert.Equal(winnerId, recorded.ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Approve, recorded.Verdict);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelWithDecisionAsync_PendingRequest_DeniesAndRecordsTheApproversDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // A managing approver retracting a request records a Deny so the audit trail names them, unlike the
        // requester's own cancellation which writes no decision.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        await accessRequestRepository.CancelWithDecisionAsync(
            request, BuildHumanDecision(request.Id, approverId, AccessDecisionVerdict.Deny, "retracted", now), now);

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Denied, persisted!.Action);
        Assert.NotNull(persisted.ActionDate);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        var recorded = Assert.Single(details!.Decisions);
        Assert.Equal(AccessDeciderKind.Human, recorded.DeciderKind);
        Assert.Equal(approverId, recorded.ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Deny, recorded.Verdict);
        Assert.Equal("retracted", recorded.Comment);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelWithDecisionAsync_ActivatedRequest_LeavesItUntouchedAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Retraction stops at activation (revoke the lease instead), and because the transition did not happen the
        // approver's Deny is not recorded either — a no-op must not orphan a decision against a live approval.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        var (approved, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        await accessRequestRepository.CancelWithDecisionAsync(
            approved,
            BuildHumanDecision(approved.Id, approverId, AccessDecisionVerdict.Deny, "too late", now.AddMinutes(5)),
            now.AddMinutes(5));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        Assert.Equal(AccessLeaseAction.None, (await accessLeaseRepository.GetByIdAsync(lease.Id))!.Action);

        // No decision was orphaned against the request the call refused to retract.
        var details = await accessRequestRepository.GetDetailsByIdAsync(approved.Id, now);
        Assert.Empty(details!.Decisions);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_ResolvedOrOtherCipher_ReturnsNull(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The read gates "you already have a request in flight for this cipher", so it must see only the caller's own
        // unresolved request for that exact cipher.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var pending = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));

        // Another cipher has no request in flight.
        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            requesterId, Guid.NewGuid(), now));

        // Another user's pending request for the same cipher is not the caller's.
        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            Guid.NewGuid(), pending.CipherId, now));

        // Once resolved, the request is no longer in flight.
        var resolved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now));
        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            requesterId, resolved.CipherId, now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxByCollectionIdsAsync_NoCollectionIds_ReturnsEmpty(
        IAccessRequestRepository accessRequestRepository)
    {
        // An approver who manages no collections has an empty inbox, rather than a query issued with an empty
        // table-valued parameter (Dapper) or an empty Contains (EF).
        Assert.Empty(await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([], DateTime.UtcNow));
        Assert.Empty(await accessRequestRepository.GetManyInboxHistoryByCollectionIdsAsync(
            [], DateTime.UtcNow.AddDays(-90), DateTime.UtcNow));
    }

    private static AccessDecision BuildHumanDecision(
        Guid accessRequestId, Guid approverId, AccessDecisionVerdict verdict, string comment, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
            AccessRequestId = accessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approverId,
            Verdict = verdict,
            Comment = comment,
            CreationDate = now,
        };

    // Creates an approved request with an open window and activates it, so the request has produced a live lease.
    private static async Task<(AccessRequest Request, AccessLease Lease)> CreateActivatedRequestAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        Guid organizationId, Guid collectionId, DateTime now)
    {
        var request = BuildRequest(organizationId, collectionId, Guid.NewGuid(), AccessRequestAction.Approved, now);
        request.NotBefore = now.AddHours(-1);
        request.NotAfter = now.AddHours(1);
        request = await accessRequestRepository.CreateAsync(request);

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        return (request, lease);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_ReturnsDenormalizedFieldsAndDecisionLog(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The dedicated request page reads one request by id with the same denormalized projection the inbox reads use
        // (requester identity) plus the full decision log — unlike the caller-scoped "mine" read which omits the
        // requester display-name join.
        var requester = await userRepository.CreateTestUserAsync("requester");
        var approver = await userRepository.CreateTestUserAsync("approver");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requester.Id, AccessRequestAction.None, now));

        await accessRequestRepository.ResolveWithDecisionAsync(
            request,
            new AccessDecision
            {
                Id = CombGuid.Generate(),
                AccessRequestId = request.Id,
                DeciderKind = AccessDeciderKind.Human,
                ApproverId = approver.Id,
                Verdict = AccessDecisionVerdict.Approve,
                Comment = "approved for audit",
                CreationDate = now,
            },
            AccessRequestAction.Approved,
            now);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);

        Assert.NotNull(details);
        Assert.Equal(request.Id, details!.Id);
        Assert.Equal(AccessRequestStatus.Approved, details.Status);
        // The denormalized requester identity is populated (unlike the caller-scoped "mine" read).
        Assert.Equal(requester.Name, details.RequesterName);
        Assert.Equal(requester.Email, details.RequesterEmail);
        // The full decision log projects with the human approver's resolved identity.
        var decision = Assert.Single(details.Decisions);
        Assert.Equal(AccessDeciderKind.Human, decision.DeciderKind);
        Assert.Equal(approver.Id, decision.ApproverId!.Value);
        Assert.Equal(approver.Email, decision.Email);
        Assert.Equal("approved for audit", decision.Comment);
        Assert.Equal(AccessDecisionVerdict.Approve, decision.Verdict);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetDetailsByIdAsync_UnknownId_ReturnsNull(IAccessRequestRepository accessRequestRepository)
    {
        Assert.Null(await accessRequestRepository.GetDetailsByIdAsync(Guid.NewGuid(), DateTime.UtcNow));
    }

    private static AccessRequest BuildRequest(
        Guid organizationId, Guid collectionId, Guid requesterId, AccessRequestAction action, DateTime creationDate)
        => new()
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = Guid.NewGuid(),
            RequesterId = requesterId,
            NotBefore = creationDate.AddHours(1),
            NotAfter = creationDate.AddHours(2),
            Reason = "audit",
            Action = action,
            CreationDate = creationDate,
            ActionDate = action == AccessRequestAction.None ? null : creationDate,
        };
}
