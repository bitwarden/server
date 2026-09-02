using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class LeaseRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAutoApprovedAsync_PersistsApprovedRequestAndDecisionWithoutLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, _) = BuildAutoApproved(organization.Id, cipherId, requesterId, now, now.AddHours(1));
        // Exercise the TINYINT ConditionKind column end-to-end: the INSERT throws if the sproc param / column type
        // does not accept the byte-backed enum value.
        decision.ConditionKind = AccessConditionKind.IpAllowlist;

        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        // The request is persisted already resolved as Approved...
        var persistedRequest = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persistedRequest);
        Assert.Equal(AccessRequestStatus.Approved, persistedRequest!.Status);
        Assert.NotNull(persistedRequest.ResolvedDate);

        // ...but no lease is minted at submit: the requester activates the approved request to start one.
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_WithinWindow_ReturnsLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now);

        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_OutsideWindow_ReturnsNull(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        // A lease whose window has already elapsed. It is minted while the window was still open (now - 2h), then
        // read back at now, by which point it has expired.
        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddHours(-2), now.AddHours(-1));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, request, decision, lease, now.AddHours(-2));

        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now);

        Assert.Null(active);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_ReturnsPendingRequest(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var request = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organization.Id,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = now.AddHours(1),
            NotAfter = now.AddHours(2),
            Reason = "audit",
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
        });

        var pending = await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(requesterId, cipherId);

        Assert.NotNull(pending);
        Assert.Equal(request.Id, pending!.Id);
        Assert.Equal("audit", pending.Reason);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyActiveByRequesterIdAsync_ReturnsOnlyActiveLeasesInWindow(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        // Active, in-window lease for the requester.
        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, activeReq, activeDec, activeLease, now);

        // Expired lease for the same requester — must be excluded.
        var (expiredReq, expiredDec, expiredLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddHours(-2), now.AddHours(-1));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, expiredReq, expiredDec, expiredLease, now.AddHours(-2));

        // Active lease for a different requester — must be excluded.
        var (otherReq, otherDec, otherLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, otherReq, otherDec, otherLease, now);

        var result = await accessLeaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now);

        Assert.Single(result);
        Assert.Equal(activeLease.Id, result.First().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_RevokesLeaseAndRecordsAuditDecision(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        var auditDecision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = revokerId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "policy change",
            CreationDate = now,
        };

        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Revoked, auditDecision, now);

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessLeaseStatus.Revoked, persisted!.Status);
        Assert.Equal(revokerId, persisted.RevokedBy);
        Assert.NotNull(persisted.RevokedDate);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_ApprovedOpenWindow_MintsActiveLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var request = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));

        // Activation has not happened yet, so the request has produced nothing.
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));

        var lease = BuildLeaseFor(request, now);
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        var produced = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(produced);
        Assert.Equal(lease.Id, produced!.Id);
        Assert.Equal(AccessLeaseStatus.Active, produced.Status);
        // The minted lease spans the request's approved window exactly — compare against the persisted request,
        // since the in-memory entity keeps tick precision the driver's datetime parameters do not.
        var persistedRequest = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.Equal(persistedRequest!.NotBefore, produced.NotBefore);
        Assert.Equal(persistedRequest.NotAfter, produced.NotAfter);

        // The requester now holds access through the standard active-lease read.
        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(
            request.RequesterId, request.CipherId, now);
        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_SecondActivation_PreconditionFailedAndKeepsFirstLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var request = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));

        var first = BuildLeaseFor(request, now);
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(first, now, false));

        // A request authorizes access at most once: the second insert is refused by the guard (and would be by the
        // unique index even if the guard raced).
        var second = BuildLeaseFor(request, now);
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(second, now, false));

        var produced = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.Equal(first.Id, produced!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_PreconditionNoLongerHolds_PreconditionFailed(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        // Still pending: not an approval.
        var pending = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), AccessRequestStatus.Pending);
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(pending, now), now, false));

        // Someone else's request: the requester filter refuses it.
        var approved = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));
        var foreign = BuildLeaseFor(approved, now);
        foreign.RequesterId = Guid.NewGuid();
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(foreign, now, false));

        // Window not started yet.
        var future = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(1), now.AddHours(2));
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(future, now), now, false));

        // Window already ended.
        var lapsed = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(lapsed, now), now, false));

        // None of the refused activations left a lease behind.
        foreach (var requestId in new[] { pending.Id, approved.Id, future.Id, lapsed.Id })
        {
            Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(requestId));
        }
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_EnforceSingleActiveLease_SecondCipherActivationConflicts(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();

        // Two different users each hold an approved request for the SAME cipher. With enforcement on, only one of them
        // may mint an active lease — contention is purely per-cipher across all users.
        var first = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);
        var second = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);

        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(first, now), now, true));

        // The cipher already has an active in-window lease, so the second activation is refused as a conflict.
        Assert.Equal(AccessLeaseMintOutcome.SingleActiveLeaseConflict,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(second, now), now, true));

        // The conflict left no lease behind for the second request.
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(second.Id));
    }

    // The same per-cipher contention under real concurrency: two users activate approved requests for one cipher at
    // the same instant on separate connections. Serializable isolation makes the loser a candidate for a provider
    // serialization failure at commit rather than a clean refusal, so this is the guard for the mint's retry -- the
    // loser must still report the conflict, and the cipher must end up carrying exactly one lease.
    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_ConcurrentSameCipherActivations_OneMintsAndTheOtherConflicts(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var first = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);
        var second = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);

        var outcomes = await Task.WhenAll(
            accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(first, now), now, true),
            accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(second, now), now, true));

        Assert.Single(outcomes, outcome => outcome == AccessLeaseMintOutcome.Minted);
        Assert.Single(outcomes, outcome => outcome == AccessLeaseMintOutcome.SingleActiveLeaseConflict);

        // Whichever request won, only its lease exists: the refused activation left nothing behind.
        var minted = outcomes[0] == AccessLeaseMintOutcome.Minted ? first : second;
        var refused = outcomes[0] == AccessLeaseMintOutcome.Minted ? second : first;
        Assert.NotNull(await accessLeaseRepository.GetByAccessRequestIdAsync(minted.Id));
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(refused.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyActiveByCollectionIdsAsync_ReturnsActiveInWindowLeasesOnGivenCollections(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        // Two active, in-window leases on distinct collections — both visible to a manager of those collections.
        var (req1, dec1, lease1) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req1, dec1, lease1, now);
        var (req2, dec2, lease2) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req2, dec2, lease2, now);

        // Active but already out of window (minted in a past window) on a third collection — excluded by the window.
        var (req3, dec3, lease3) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-2), now.AddHours(-1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req3, dec3, lease3, now.AddHours(-2));

        var all = await accessLeaseRepository.GetManyActiveByCollectionIdsAsync(
            new[] { lease1.CollectionId, lease2.CollectionId, lease3.CollectionId }, now);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, l => l.Id == lease1.Id);
        Assert.Contains(all, l => l.Id == lease2.Id);

        // Collection scoping: querying a subset returns only that collection's leases.
        var scoped = await accessLeaseRepository.GetManyActiveByCollectionIdsAsync(new[] { lease1.CollectionId }, now);
        Assert.Single(scoped);
        Assert.Equal(lease1.Id, scoped.First().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyEndedByCollectionIdsAsync_ReturnsRecentlyEndedLeasesOnGivenCollections(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var since = now.AddDays(-90);

        // Active lease — not ended, excluded.
        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, activeReq, activeDec, activeLease, now);

        // Revoked within the window — included.
        var (revReq, revDec, revLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, revReq, revDec, revLease, now);
        await accessLeaseRepository.RevokeAsync(revLease, AccessLeaseStatus.Revoked, BuildAuditDecision(revLease, now), now);

        // Revoked long before the window — excluded by @Since.
        var (oldReq, oldDec, oldLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-200), now.AddDays(-100));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, oldReq, oldDec, oldLease, now.AddDays(-200));
        await accessLeaseRepository.RevokeAsync(oldLease, AccessLeaseStatus.Revoked, BuildAuditDecision(oldLease, now.AddDays(-150)), now.AddDays(-150));

        var result = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { activeLease.CollectionId, revLease.CollectionId, oldLease.CollectionId }, since);

        Assert.Single(result);
        Assert.Equal(revLease.Id, result.First().Id);
        Assert.Equal(AccessLeaseStatus.Revoked, result.First().Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_AlreadyEndedLease_EndsNothingAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // The end is guarded on Active, so a repeat (or losing) revoke must leave both halves alone: the first
        // revoker's identity survives, and no second Deny is appended to a lease this call did not end. Without the
        // guard the decision log would accumulate a verdict for every attempt.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var firstRevokerId = Guid.NewGuid();
        var secondRevokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        // The auto-approval already recorded one automatic decision against the request.
        var beforeRevoke = await accessRequestRepository.GetDetailsByIdAsync(request.Id);
        Assert.Single(beforeRevoke!.Decisions);

        var first = BuildAuditDecision(lease, now);
        first.ApproverId = firstRevokerId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Revoked, first, now);

        var afterFirst = await accessRequestRepository.GetDetailsByIdAsync(request.Id);
        Assert.Equal(2, afterFirst!.Decisions.Count);

        // A second revoke finds the lease already ended.
        var second = BuildAuditDecision(lease, now.AddMinutes(1));
        second.ApproverId = secondRevokerId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Cancelled, second, now.AddMinutes(1));

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseStatus.Revoked, persisted!.Status);
        Assert.Equal(firstRevokerId, persisted.RevokedBy);

        // No verdict was appended for the lease the second call did not end.
        var afterSecond = await accessRequestRepository.GetDetailsByIdAsync(request.Id);
        Assert.Equal(2, afterSecond!.Decisions.Count);
        Assert.DoesNotContain(afterSecond.Decisions, d => d.ApproverId == secondRevokerId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_StaleCallerRequestId_RecordsDecisionAgainstTheLeasesOwnRequest(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // The audit decision belongs to the request the lease actually came from, so the request id is read from the
        // lease row rather than trusted from the caller's (possibly stale) copy. A caller passing a wrong request id
        // must not be able to file the verdict against an unrelated request.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        // An unrelated request that must not collect the verdict.
        var (otherRequest, otherDecision, _) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await accessRequestRepository.CreateAutoApprovedAsync(otherRequest, otherDecision);

        // The caller's copy of the lease points at the wrong request, as does the decision it supplies.
        var staleLease = BuildLeaseFor(request, now);
        staleLease.Id = lease.Id;
        staleLease.AccessRequestId = otherRequest.Id;

        var auditDecision = BuildAuditDecision(lease, now);
        auditDecision.AccessRequestId = otherRequest.Id;
        auditDecision.ApproverId = revokerId;

        await accessLeaseRepository.RevokeAsync(staleLease, AccessLeaseStatus.Revoked, auditDecision, now);

        // The verdict landed on the lease's real originating request...
        var owning = await accessRequestRepository.GetDetailsByIdAsync(request.Id);
        Assert.Equal(2, owning!.Decisions.Count);
        Assert.Contains(owning.Decisions, d => d.ApproverId == revokerId);

        // ...and not on the request the caller named.
        var unrelated = await accessRequestRepository.GetDetailsByIdAsync(otherRequest.Id);
        Assert.Single(unrelated!.Decisions);
        Assert.DoesNotContain(unrelated.Decisions, d => d.ApproverId == revokerId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_HolderEndsOwnLease_EndsAsCancelled(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Cancelled is the end state when the holder ended their own lease, as opposed to Revoked when an operator
        // did. Both travel the same write path, so the end status must round-trip rather than being forced to Revoked.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        var auditDecision = BuildAuditDecision(lease, now);
        auditDecision.ApproverId = requesterId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Cancelled, auditDecision, now);

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseStatus.Cancelled, persisted!.Status);
        Assert.Equal(requesterId, persisted.RevokedBy);
        Assert.NotNull(persisted.RevokedDate);

        // A cancelled lease no longer grants access...
        Assert.Null(await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now));
        Assert.Empty(await accessLeaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now));

        // ...and it counts as ended for the governance history view.
        var ended = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { lease.CollectionId }, now.AddDays(-1));
        Assert.Equal(AccessLeaseStatus.Cancelled, Assert.Single(ended).Status);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyEndedByCollectionIdsAsync_OrdersByEndDateMostRecentFirst(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // A revoked/cancelled lease's end is its revoked date, and the history view is ordered by that end most
        // recently ended first — so the ordering key is not the lease's creation or window.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var collectionId = Guid.NewGuid();

        // Seeded oldest-first, but ended in the reverse order, so creation order cannot stand in for end order.
        var (firstReq, firstDec, firstLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-3), now.AddHours(1));
        firstLease.CollectionId = collectionId;
        firstReq.CollectionId = collectionId;
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, firstReq, firstDec, firstLease, now.AddHours(-3));

        var (secondReq, secondDec, secondLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-2), now.AddHours(1));
        secondLease.CollectionId = collectionId;
        secondReq.CollectionId = collectionId;
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, secondReq, secondDec, secondLease, now.AddHours(-2));

        // The lease created second ends first, so it must sort last.
        await accessLeaseRepository.RevokeAsync(
            secondLease, AccessLeaseStatus.Cancelled, BuildAuditDecision(secondLease, now.AddHours(-1)), now.AddHours(-1));
        await accessLeaseRepository.RevokeAsync(
            firstLease, AccessLeaseStatus.Revoked, BuildAuditDecision(firstLease, now), now);

        var ended = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { collectionId }, now.AddDays(-1));

        Assert.Equal(2, ended.Count);
        Assert.Equal(firstLease.Id, ended.First().Id);
        Assert.Equal(secondLease.Id, ended.Last().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByCollectionIdsAsync_NoCollectionIds_ReturnsEmpty(
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Both collection-scoped governance reads short-circuit on an empty set rather than issuing a query with an
        // empty table-valued parameter (Dapper) or an empty Contains (EF).
        var now = DateTime.UtcNow;

        Assert.Empty(await accessLeaseRepository.GetManyActiveByCollectionIdsAsync([], now));
        Assert.Empty(await accessLeaseRepository.GetManyEndedByCollectionIdsAsync([], now.AddDays(-1)));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetByIdAsync_UnknownId_ReturnsNull(IAccessLeaseRepository accessLeaseRepository)
    {
        Assert.Null(await accessLeaseRepository.GetByIdAsync(Guid.NewGuid()));
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(Guid.NewGuid()));
    }

    private static AccessDecision BuildAuditDecision(AccessLease lease, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "ended for test",
            CreationDate = now,
        };

    private static async Task<AccessRequest> CreateApprovedRequestAsync(
        IAccessRequestRepository accessRequestRepository, Guid organizationId, DateTime notBefore, DateTime notAfter,
        AccessRequestStatus status = AccessRequestStatus.Approved, Guid? cipherId = null)
        => await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId ?? Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = notBefore,
            NotAfter = notAfter,
            Reason = "audit",
            Status = status,
            CreationDate = DateTime.UtcNow,
            ResolvedDate = status == AccessRequestStatus.Pending ? null : DateTime.UtcNow,
        });

    // Seeds an active lease the way production now does: record the approved request, then mint the lease by
    // activating it. The mint time sits inside the request's window (it can be in the past), so leases whose windows
    // have already elapsed by read time can still be seeded for the read-path tests.
    private static async Task SeedActiveLeaseAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        AccessRequest request, AccessDecision decision, AccessLease lease, DateTime mintTime)
    {
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        // Assert the mint rather than discarding it: several callers seed a row they expect to be *excluded* from a
        // read, and without this those assertions would pass vacuously if the mint had silently failed.
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, mintTime, false));
    }

    private static AccessLease BuildLeaseFor(AccessRequest request, DateTime now)
        => new()
        {
            Id = CombGuid.Generate(),
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

    private static (AccessRequest, AccessDecision, AccessLease) BuildAutoApproved(
        Guid organizationId, Guid cipherId, Guid requesterId, DateTime notBefore, DateTime notAfter)
    {
        var collectionId = Guid.NewGuid();
        var request = new AccessRequest
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Status = AccessRequestStatus.Approved,
        };
        var decision = new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
        };
        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = cipherId,
            RequesterId = requesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = notBefore,
            NotAfter = notAfter,
        };
        return (request, decision, lease);
    }
}
