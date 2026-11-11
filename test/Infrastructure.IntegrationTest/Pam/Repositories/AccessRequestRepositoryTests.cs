using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
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
    public async Task GetHistoryPageAsync_ReturnsOnlyEndedRequestsInTheWindow(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now));
        // Approved and still inside its window, open, and ended before the window: none of them is history.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now));
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now.AddDays(-120)));

        var page = await ReadHistoryAsync(accessRequestRepository, [collection.Id], now);

        Assert.Equal(denied.Id, Assert.Single(page.Requests).Id);
        Assert.Equal(1, page.Total);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetHistoryPageAsync_PagesByEndNewestFirstWithoutGapsOrRepeats(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Created in one order, ended in the reverse one; two end at the same instant.
        var endedAt = new[] { 9, 2, 2, 5, 1 };
        var created = new List<Guid>();
        for (var i = 0; i < endedAt.Length; i++)
        {
            var request = BuildRequest(
                organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now.AddMinutes(-60 + i));
            request.ActionDate = now.AddMinutes(-endedAt[i]);
            created.Add((await accessRequestRepository.CreateAsync(request)).Id);
        }

        var walked = new List<AccessRequestDetails>();
        for (var skip = 0; skip < endedAt.Length; skip += 2)
        {
            var page = await ReadHistoryAsync(accessRequestRepository, [collection.Id], now, skip: skip, take: 2);
            Assert.Equal(endedAt.Length, page.Total);
            walked.AddRange(page.Requests);
        }

        Assert.Equal(created.Order(), walked.Select(r => r.Id).Order());
        Assert.Equal(walked.Count, walked.Select(r => r.Id).Distinct().Count());
        Assert.True(walked.Zip(walked.Skip(1)).All(pair => pair.First.ActionDate >= pair.Second.ActionDate));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetHistoryPageAsync_DerivesHowEachRequestEnded(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now));
        var canceled = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Cancelled, now));
        // BuildRequest's window closes two hours after creation.
        var unanswered = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));
        var unstarted = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now.AddHours(-3)));
        var (ranOut, _) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-3));
        var (revoked, revokedLease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);
        await accessLeaseRepository.RevokeAsync(revokedLease, AccessLeaseAction.Revoked,
            BuildHumanDecision(revoked.Id, Guid.NewGuid(), AccessDecisionVerdict.Deny, "revoked", now), now);
        var (ended, endedLease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);
        await accessLeaseRepository.RevokeAsync(endedLease, AccessLeaseAction.Cancelled,
            BuildHumanDecision(ended.Id, ended.RequesterId, AccessDecisionVerdict.Deny, "done", now), now);
        // Still running: not history.
        await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        async Task<Guid[]> IdsWith(AccessRequestHistoryStatus status) =>
            (await ReadHistoryAsync(accessRequestRepository, [collection.Id], now, statuses: [status]))
            .Requests.Select(r => r.Id).ToArray();

        Assert.Equal([denied.Id], await IdsWith(AccessRequestHistoryStatus.Denied));
        Assert.Equal([canceled.Id], await IdsWith(AccessRequestHistoryStatus.Canceled));
        Assert.Equal(new[] { unanswered.Id, unstarted.Id }.Order(), (await IdsWith(AccessRequestHistoryStatus.Expired)).Order());
        Assert.Equal([revoked.Id], await IdsWith(AccessRequestHistoryStatus.Revoked));
        Assert.Equal(new[] { ranOut.Id, ended.Id }.Order(), (await IdsWith(AccessRequestHistoryStatus.SessionEnded)).Order());
        Assert.Equal(7, (await ReadHistoryAsync(accessRequestRepository, [collection.Id], now)).Total);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetHistoryPageAsync_KeepsOnlyADeniedExtension(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // The client folds every extension but a denied one into its lease's row, so the page must agree.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var (_, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        async Task<AccessRequest> ExtensionAsync(AccessRequestAction action, DateTime creationDate)
        {
            var extension = BuildRequest(organization.Id, collection.Id, Guid.NewGuid(), action, creationDate);
            extension.ExtensionOfLeaseId = lease.Id;
            return await accessRequestRepository.CreateAsync(extension);
        }

        var denied = await ExtensionAsync(AccessRequestAction.Denied, now);
        await ExtensionAsync(AccessRequestAction.Cancelled, now);
        // Unanswered, and its window closed an hour ago.
        await ExtensionAsync(AccessRequestAction.None, now.AddHours(-3));

        var page = await ReadHistoryAsync(accessRequestRepository, [collection.Id], now);

        Assert.Equal(denied.Id, Assert.Single(page.Requests).Id);
        Assert.Equal(1, page.Total);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetHistoryPageAsync_ReadsTheCallersOwnRequestsAndFiltersAndFacets(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var caller = await userRepository.CreateTestUserAsync("caller");
        var other = await userRepository.CreateTestUserAsync("other");
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var managed = await collectionRepository.CreateTestCollectionAsync(organization);
        var unmanaged = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var othersOnManaged = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, managed.Id, other.Id, AccessRequestAction.Denied, now));
        var ownOnUnmanaged = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, unmanaged.Id, caller.Id, AccessRequestAction.Cancelled, now));
        // Someone else's request on a collection the caller does not manage.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, unmanaged.Id, other.Id, AccessRequestAction.Denied, now));

        var all = await ReadHistoryAsync(accessRequestRepository, [managed.Id], now, userId: caller.Id);
        Assert.Equal(new[] { othersOnManaged.Id, ownOnUnmanaged.Id }.Order(), all.Requests.Select(r => r.Id).Order());
        Assert.Equal(new[] { managed.Id, unmanaged.Id }.Order(), all.CollectionIds.Order());
        Assert.Equal(new[] { caller.Email, other.Email }.Order(), all.Requesters.Select(r => r.Email).Order());

        // A filter narrows the rows and the total, never the facets.
        var byRequester = await ReadHistoryAsync(
            accessRequestRepository, [managed.Id], now, userId: caller.Id, requesterIds: [other.Id]);
        Assert.Equal(othersOnManaged.Id, Assert.Single(byRequester.Requests).Id);
        Assert.Equal(1, byRequester.Total);
        Assert.Equal(2, byRequester.Requesters.Count);
        var byCollection = await ReadHistoryAsync(
            accessRequestRepository, [managed.Id], now, userId: caller.Id, collectionIds: [unmanaged.Id]);
        Assert.Equal(ownOnUnmanaged.Id, Assert.Single(byCollection.Requests).Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxApprovedByCollectionIdsAsync_ReturnsApprovalsThatCanStillGiveAccess(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Not started, window open: in.
        var unstarted = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now));
        // Not started, window closed; denied; pending: out.
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now.AddHours(-3)));
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now));
        await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));
        // Running inside its request's window: in.
        var (running, _) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        // Window [now-3h, now-1h], activated at now-2h, extended to an hour from now: the request and its extension
        // are in, although the request's own window has closed.
        var (extended, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-2));
        var extendedAt = now.AddMinutes(-90);
        var extension = BuildRequest(
            organization.Id, collection.Id, extended.RequesterId, AccessRequestAction.Approved, extendedAt);
        extension.Id = CombGuid.Generate();
        extension.ExtensionOfLeaseId = lease.Id;
        extension.CipherId = extended.CipherId;
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

        // Ran to its end: out.
        await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-3));

        var approved = await accessRequestRepository.GetManyInboxApprovedByCollectionIdsAsync([collection.Id], now);

        Assert.Equal(
            new[] { unstarted.Id, running.Id, extended.Id, extension.Id }.Order(),
            approved.Select(r => r.Id).Order());
        var extendedRow = approved.Single(r => r.Id == extended.Id);
        Assert.Equal(lease.Id, extendedRow.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, extendedRow.ProducedLeaseStatus);
        Assert.Empty(await accessRequestRepository.GetManyInboxApprovedByCollectionIdsAsync([Guid.NewGuid()], now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ProducedLeaseStatus_ActiveOnTheApprovedRead_RevokedInTheHistory(
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

        // The client offers Revoke while the produced lease reads Active.
        var active = Assert.Single(await accessRequestRepository.GetManyInboxApprovedByCollectionIdsAsync(
            [collection.Id], now));
        Assert.Equal(lease.Id, active.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, active.ProducedLeaseStatus);

        // Once revoked the window is unchanged, so the status is what stops the client offering a Revoke the server
        // would reject.
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

        var revoked = Assert.Single(
            (await ReadHistoryAsync(accessRequestRepository, [collection.Id], now)).Requests);
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
        // Expiry is never stored; only a projection against the read clock derives it, on all three reads.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Activated three hours ago on a window that closed two hours ago.
        var (request, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-3));

        var stored = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, stored!.Action);
        Assert.Null(stored.RevokedDate);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(lease.Id, details!.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Expired, details.ProducedLeaseStatus);

        var mine = Assert.Single(await accessRequestRepository.GetManyByRequesterIdAsync(
            request.RequesterId, now.AddDays(-1), now));
        Assert.Equal(AccessLeaseStatus.Expired, mine.ProducedLeaseStatus);

        var history = Assert.Single((await ReadHistoryAsync(accessRequestRepository, [collection.Id], now)).Requests);
        Assert.Equal(AccessLeaseStatus.Expired, history.ProducedLeaseStatus);

        // Read inside the window, the same row is Active; this is a projection, not a write.
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
        // An extension moves the lease's NotAfter but not the request's, so the projection must read the lease's.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        // Window [now-3h, now-1h], activated at now-2h.
        var (request, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now.AddHours(-2));

        // Extended by two hours while still live, so the lease now ends an hour from now.
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

        Assert.True(request.NotAfter < now);
        var extended = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.True(extended!.NotAfter > now);

        // The client counts down from the lease's own end, not the request's lapsed window.
        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(lease.Id, details!.ProducedLeaseId);
        Assert.Equal(AccessLeaseStatus.Active, details.ProducedLeaseStatus);
        Assert.Equal(extended.NotAfter, details.ProducedLeaseNotAfter!.Value, LaxDateTimeComparer.Default);
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

        Assert.True(await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestAction.Approved, now));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        Assert.NotNull(persisted.ActionDate);

        // Approved and not started, so the approved read holds it, not the history.
        var row = Assert.Single(await accessRequestRepository.GetManyInboxApprovedByCollectionIdsAsync(
            [collection.Id], now));
        var recorded = Assert.Single(row.Decisions);
        Assert.Equal(AccessDeciderKind.Human, recorded.DeciderKind);
        Assert.Equal(approverId, recorded.ApproverId!.Value);
        Assert.Equal("approved for audit", recorded.Comment);
        Assert.Equal(AccessDecisionVerdict.Approve, recorded.Verdict);
        // Dapper binds DateTime as DbType.DateTime (3.33 ms) on MSSQL and the EF providers store microseconds.
        Assert.Equal(now, recorded.DecidedAt, LaxDateTimeComparer.Default);
        // The approver id matches no User row, so the identity join yields nulls and the client falls back to the id.
        Assert.Null(recorded.Name);
        Assert.Null(recorded.Email);

        // Approval records only the verdict; no lease exists until the requester activates the request.
        Assert.Null(row.ProducedLeaseId);
        Assert.Null(row.ProducedLeaseStatus);
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
        Assert.Null(await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(
            request.RequesterId, request.CipherId, now));

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

        Assert.True(await accessRequestRepository.ResolveWithDecisionAsync(request, decision, AccessRequestAction.Denied, now));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Denied, persisted!.Action);

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

        var openWindow = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now);
        openWindow.NotBefore = now.AddHours(-1);
        openWindow.NotAfter = now.AddHours(1);
        var startable = await accessRequestRepository.CreateAsync(openWindow);

        var found = await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, startable.CipherId, now);
        Assert.NotNull(found);
        Assert.Equal(startable.Id, found!.Id);

        // A future window is included, since the client shows the upcoming window.
        var future = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now));
        Assert.NotNull(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, future.CipherId, now));

        // A lapsed window is excluded, since it can never be activated.
        var lapsed = BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now);
        lapsed.NotBefore = now.AddHours(-2);
        lapsed.NotAfter = now.AddHours(-1);
        lapsed = await accessRequestRepository.CreateAsync(lapsed);
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, lapsed.CipherId, now));

        var pending = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, pending.CipherId, now));
        var denied = await accessRequestRepository.CreateAsync(
            BuildRequest(organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now));
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, denied.CipherId, now));

        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            Guid.NewGuid(), startable.CipherId, now));

        // Once the approval produces a lease it counts as activated and leaves this read.
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
        // The requester's list takes the approver history's 90-day window, but only over rows that are history.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var since = now.AddDays(-90);
        var requesterId = Guid.NewGuid();

        var recentlyDenied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now.AddDays(-2)));
        var longAgoDenied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now.AddDays(-120)));

        // An open request is live whatever its age; windowing it away would drop it, not age it out.
        var longOpenStillAnswerable = BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now.AddDays(-120));
        longOpenStillAnswerable.NotBefore = now.AddHours(-1);
        longOpenStillAnswerable.NotAfter = now.AddHours(1);
        longOpenStillAnswerable = await accessRequestRepository.CreateAsync(longOpenStillAnswerable);

        // A lapsed unanswered request is derived Expired, so it ages out as history like any resolved row.
        var longLapsedUnanswered = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now.AddDays(-120)));

        // An approved request whose window is still open is still activatable, whatever its age.
        var stillActivatable = BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now.AddDays(-120));
        stillActivatable.NotBefore = now.AddHours(-1);
        stillActivatable.NotAfter = now.AddHours(1);
        stillActivatable = await accessRequestRepository.CreateAsync(stillActivatable);

        // An approval whose window closed before it was activated is history like any other.
        var lapsedApproved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Approved, now.AddDays(-120)));

        var windowed = await accessRequestRepository.GetManyByRequesterIdAsync(requesterId, since, now);

        Assert.Contains(windowed, r => r.Id == recentlyDenied.Id);
        Assert.Contains(windowed, r => r.Id == longOpenStillAnswerable.Id);
        Assert.Contains(windowed, r => r.Id == stillActivatable.Id);
        Assert.DoesNotContain(windowed, r => r.Id == longAgoDenied.Id);
        Assert.DoesNotContain(windowed, r => r.Id == longLapsedUnanswered.Id);
        Assert.DoesNotContain(windowed, r => r.Id == lapsedApproved.Id);

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
        // The requester's own list omits the vault-derived name joins but resolves the approver from User, so the
        // client shows a name instead of a raw id.
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
    public async Task GetHistoryPageAsync_MultipleHumanDecisions_ProjectsFullHistoryOldestFirst(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // A request has many decisions, so an approval and a later retraction both surface rather than collapsing to a
        // single resolver.
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

        var row = Assert.Single((await ReadHistoryAsync(accessRequestRepository, [collection.Id], now)).Requests);
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
        Assert.True(await accessRequestRepository.CancelAsync(request.Id, resolvedAt));

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

        // Denied rather than Approved, since an approved request stays cancellable until it is activated.
        var denied = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Denied, now));

        Assert.False(await accessRequestRepository.CancelAsync(denied.Id, now.AddMinutes(5)));

        var persisted = await accessRequestRepository.GetByIdAsync(denied.Id);
        Assert.Equal(AccessRequestAction.Denied, persisted!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CancelAsync_ApprovedUnactivatedRequest_TransitionsToCancelled(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The requester may also withdraw an approval not yet activated, so no lease is ever minted from it.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var approved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now));

        Assert.True(await accessRequestRepository.CancelAsync(approved.Id, now.AddMinutes(5)));

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
        // An activated request's access is governed by its lease, which is revoked instead; cancelling the request
        // would strand a live lease.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var (approved, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        Assert.False(await accessRequestRepository.CancelAsync(approved.Id, now.AddMinutes(5)));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        var persistedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, persistedLease!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxPendingByCollectionIdsAsync_LapsedUnansweredRow_LeavesInboxAndIsHistoryAsExpired(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // Nothing writes Expired; the read's @Now comparison alone moves a lapsed row from inbox to history.
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

        var historyRow = Assert.Single((await ReadHistoryAsync(accessRequestRepository, [collection.Id], now)).Requests);
        Assert.Equal(lapsed.Id, historyRow.Id);
        Assert.Equal(AccessRequestStatus.Expired, historyRow.Status);
        Assert.Null(historyRow.ActionDate); // nobody acted; the end time is NotAfter, not a resolution
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_LapsedUnanswered_ReturnsNull(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // A lapsed unanswered request is derived Expired, so it must not block a fresh submission.
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
        // A row already derived Expired must never restamp to Cancelled; the write's own @Now guard decides.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));

        Assert.False(await accessRequestRepository.CancelAsync(lapsed.Id, now));

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
        // The same @Now guard keeps a lapsed, unactivated approval Expired rather than restamping it Denied.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.Approved, now.AddHours(-3)));

        Assert.False(await accessRequestRepository.CancelWithDecisionAsync(
            lapsed, BuildHumanDecision(lapsed.Id, Guid.NewGuid(), AccessDecisionVerdict.Deny, "too late", now), now));

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
        // Two approvers race on one pending request. The loser's verdict is not appended, or the decision log would
        // contradict the request's status.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var winnerId = Guid.NewGuid();
        var loserId = Guid.NewGuid();

        var request = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now));

        Assert.True(await accessRequestRepository.ResolveWithDecisionAsync(
            request, BuildHumanDecision(request.Id, winnerId, AccessDecisionVerdict.Approve, "approved", now),
            AccessRequestAction.Approved, now));

        Assert.False(await accessRequestRepository.ResolveWithDecisionAsync(
            request, BuildHumanDecision(request.Id, loserId, AccessDecisionVerdict.Deny, "denied", now.AddMinutes(1)),
            AccessRequestAction.Denied, now.AddMinutes(1)));

        var persisted = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        var recorded = Assert.Single(details!.Decisions);
        Assert.Equal(winnerId, recorded.ApproverId!.Value);
        Assert.Equal(AccessDecisionVerdict.Approve, recorded.Verdict);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ResolveWithDecisionAsync_LapsedRequest_LeavesItUntouchedAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // An unanswered request whose window has lapsed reads Expired; a late verdict must not restamp it.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var lapsed = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, Guid.NewGuid(), AccessRequestAction.None, now.AddHours(-3)));

        Assert.False(await accessRequestRepository.ResolveWithDecisionAsync(
            lapsed, BuildHumanDecision(lapsed.Id, Guid.NewGuid(), AccessDecisionVerdict.Deny, "too late", now),
            AccessRequestAction.Denied, now));

        var persisted = await accessRequestRepository.GetByIdAsync(lapsed.Id);
        Assert.Equal(AccessRequestAction.None, persisted!.Action);
        Assert.Null(persisted.ActionDate);

        var details = await accessRequestRepository.GetDetailsByIdAsync(lapsed.Id, now);
        Assert.Equal(AccessRequestStatus.Expired, details!.Status);
        Assert.Empty(details.Decisions);
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

        Assert.True(await accessRequestRepository.CancelWithDecisionAsync(
            request, BuildHumanDecision(request.Id, approverId, AccessDecisionVerdict.Deny, "retracted", now), now));

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
        // Retraction stops at activation, and the refused call must not orphan a Deny against a live approval.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var approverId = Guid.NewGuid();

        var (approved, lease) = await CreateActivatedRequestAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, now);

        Assert.False(await accessRequestRepository.CancelWithDecisionAsync(
            approved,
            BuildHumanDecision(approved.Id, approverId, AccessDecisionVerdict.Deny, "too late", now.AddMinutes(5)),
            now.AddMinutes(5)));

        var persisted = await accessRequestRepository.GetByIdAsync(approved.Id);
        Assert.Equal(AccessRequestAction.Approved, persisted!.Action);
        Assert.Equal(AccessLeaseAction.None, (await accessLeaseRepository.GetByIdAsync(lease.Id))!.Action);

        var details = await accessRequestRepository.GetDetailsByIdAsync(approved.Id, now);
        Assert.Empty(details!.Decisions);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_ResolvedOrOtherCipher_ReturnsNull(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        // The read gates "you already have a request in flight for this cipher", so it sees only the caller's own
        // unresolved request for that cipher.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var pending = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.None, now));

        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            requesterId, Guid.NewGuid(), now));

        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            Guid.NewGuid(), pending.CipherId, now));

        var resolved = await accessRequestRepository.CreateAsync(BuildRequest(
            organization.Id, collection.Id, requesterId, AccessRequestAction.Denied, now));
        Assert.Null(await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(
            requesterId, resolved.CipherId, now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyInboxByCollectionIdsAsync_NoCollectionIds_ReturnsEmpty(
        IAccessRequestRepository accessRequestRepository)
    {
        // The inbox reads short-circuit on an empty set instead of querying with an empty TVP (Dapper) or Contains
        // (EF); the history still reads the caller's own requests.
        Assert.Empty(await accessRequestRepository.GetManyInboxPendingByCollectionIdsAsync([], DateTime.UtcNow));
        Assert.Empty(await accessRequestRepository.GetManyInboxApprovedByCollectionIdsAsync([], DateTime.UtcNow));
        var history = await ReadHistoryAsync(accessRequestRepository, [], DateTime.UtcNow);
        Assert.Empty(history.Requests);
        Assert.Equal(0, history.Total);
    }

    private static Task<AccessRequestHistoryPage> ReadHistoryAsync(
        IAccessRequestRepository accessRequestRepository, IReadOnlyCollection<Guid> manageableCollectionIds,
        DateTime now, Guid? userId = null, int skip = 0, int take = 100,
        IReadOnlyCollection<AccessRequestHistoryStatus>? statuses = null,
        IReadOnlyCollection<Guid>? collectionIds = null, IReadOnlyCollection<Guid>? requesterIds = null)
        => accessRequestRepository.GetHistoryPageAsync(new AccessRequestHistoryFilter
        {
            ManageableCollectionIds = manageableCollectionIds,
            UserId = userId ?? Guid.NewGuid(),
            Since = now.AddDays(-90),
            Now = now,
            Skip = skip,
            Take = take,
            Statuses = statuses ?? [],
            CollectionIds = collectionIds ?? [],
            RequesterIds = requesterIds ?? [],
        });

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
        // The request page's read carries the requester identity, which the caller-scoped "mine" read omits, and the
        // full decision log.
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
        Assert.Equal(requester.Name, details.RequesterName);
        Assert.Equal(requester.Email, details.RequesterEmail);
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
