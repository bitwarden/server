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
        // The INSERT throws if the TINYINT parameter or column rejects the byte-backed enum.
        decision.ConditionKind = AccessConditionKind.IpAllowlist;

        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        var persistedRequest = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persistedRequest);
        Assert.Equal(AccessRequestAction.Approved, persistedRequest!.Action);
        Assert.NotNull(persistedRequest.ActionDate);

        // No lease is minted at submit; the requester activates the approved request to start one.
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
            CreationDate = now,
        });

        var pending = await accessRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(requesterId, cipherId, now);

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

        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, activeReq, activeDec, activeLease, now);

        var (expiredReq, expiredDec, expiredLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddHours(-2), now.AddHours(-1));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, expiredReq, expiredDec, expiredLease, now.AddHours(-2));

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

        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, auditDecision, now);

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(persisted);
        Assert.Equal(AccessLeaseAction.Revoked, persisted!.Action);
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

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id));

        var lease = BuildLeaseFor(request, now);
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));

        var produced = await accessLeaseRepository.GetByAccessRequestIdAsync(request.Id);
        Assert.NotNull(produced);
        Assert.Equal(lease.Id, produced!.Id);
        Assert.Equal(AccessLeaseAction.None, produced.Action);
        // Starts at activation, not the window start; the persisted request carries the same stored precision.
        var persistedRequest = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotEqual(persistedRequest!.NotBefore, produced.NotBefore);
        Assert.Equal(persistedRequest.NotAfter, produced.NotAfter);
        // @Now round-trips through datetime2, so compare within a tolerance rather than on exact ticks.
        Assert.Equal(now, produced.NotBefore, TimeSpan.FromSeconds(1));
        Assert.Equal(produced.CreationDate, produced.NotBefore, TimeSpan.FromSeconds(1));

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

        // The guard refuses the second insert, and the unique index would if the guard raced.
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

        var pending = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), AccessRequestAction.None);
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(pending, now), now, false));

        var approved = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1));
        var foreign = BuildLeaseFor(approved, now);
        foreign.RequesterId = Guid.NewGuid();
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(foreign, now, false));

        var future = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(1), now.AddHours(2));
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(future, now), now, false));

        var lapsed = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(lapsed, now), now, false));

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

        // Two users hold approved requests for one cipher; with enforcement on, contention is per cipher across users.
        var first = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);
        var second = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);

        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(first, now), now, true));

        Assert.Equal(AccessLeaseMintOutcome.SingleActiveLeaseConflict,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(second, now), now, true));

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(second.Id));
    }

    // Two activations for one cipher at the same instant, on separate connections. Under Serializable the loser can hit
    // a provider serialization failure at commit, so this pins that the mint's retry still reports the conflict.
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

        var (req1, dec1, lease1) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req1, dec1, lease1, now);
        var (req2, dec2, lease2) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req2, dec2, lease2, now);

        var (req3, dec3, lease3) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-2), now.AddHours(-1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req3, dec3, lease3, now.AddHours(-2));

        var all = await accessLeaseRepository.GetManyActiveByCollectionIdsAsync(
            new[] { lease1.CollectionId, lease2.CollectionId, lease3.CollectionId }, now);

        Assert.Equal(2, all.Count);
        Assert.Contains(all, l => l.Id == lease1.Id);
        Assert.Contains(all, l => l.Id == lease2.Id);

        var scoped = await accessLeaseRepository.GetManyActiveByCollectionIdsAsync(new[] { lease1.CollectionId }, now);
        Assert.Single(scoped);
        Assert.Equal(lease1.Id, scoped.First().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByCipherIdAsync_ReturnsAnotherMembersLeaseAndTheLatestEnd(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();

        Assert.Null(await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now));

        var (req1, dec1, lease1) = BuildAutoApproved(
            organization.Id, cipherId, Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req1, dec1, lease1, now);

        var found = await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now);
        Assert.NotNull(found);
        Assert.Equal(lease1.Id, found.Id);

        var (req2, dec2, lease2) = BuildAutoApproved(
            organization.Id, cipherId, Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(3));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, req2, dec2, lease2, now);

        var latest = await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now);
        Assert.NotNull(latest);
        Assert.Equal(lease2.Id, latest.Id);

        Assert.Null(await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now.AddHours(4)));

        Assert.Null(await accessLeaseRepository.GetActiveByCipherIdAsync(Guid.NewGuid(), now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByCipherIdAsync_FlipsInStepWithTheSingletonGuardItMirrors(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Pins the pre-check read to the mint guard, so a divergence in their shared predicate fails here.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();

        var holder = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(1), cipherId: cipherId);
        // Outlives the holder's window, so it is still activatable at the later instant below.
        var contender = await CreateApprovedRequestAsync(
            accessRequestRepository, organization.Id, now.AddHours(-1), now.AddHours(3), cipherId: cipherId);

        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(holder, now), now, true));

        var blocker = await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, now);
        Assert.NotNull(blocker);
        Assert.Equal(AccessLeaseMintOutcome.SingleActiveLeaseConflict,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(BuildLeaseFor(contender, now), now, true));

        // At SlotFreesAt the read and the guard flip together.
        var afterSlotFrees = blocker.NotAfter;
        Assert.Null(await accessLeaseRepository.GetActiveByCipherIdAsync(cipherId, afterSlotFrees));
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(
                BuildLeaseFor(contender, afterSlotFrees), afterSlotFrees, true));
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

        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, activeReq, activeDec, activeLease, now);

        var (revReq, revDec, revLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, revReq, revDec, revLease, now);
        await accessLeaseRepository.RevokeAsync(revLease, AccessLeaseAction.Revoked, BuildAuditDecision(revLease, now), now);

        var (oldReq, oldDec, oldLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-200), now.AddDays(-100));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, oldReq, oldDec, oldLease, now.AddDays(-200));
        await accessLeaseRepository.RevokeAsync(oldLease, AccessLeaseAction.Revoked, BuildAuditDecision(oldLease, now.AddDays(-150)), now.AddDays(-150));

        var result = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { activeLease.CollectionId, revLease.CollectionId, oldLease.CollectionId }, since, now);

        Assert.Single(result);
        Assert.Equal(revLease.Id, result.First().Id);
        Assert.Equal(AccessLeaseAction.Revoked, result.First().Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyEndedByCollectionIdsAsync_LapsedLease_IsProjectedExpiredAndIncluded(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // A naturally closed lease records no early end; ended-ness here is derived against `now`.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var (lapsedReq, lapsedDec, lapsedLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-3), now.AddHours(-1));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, lapsedReq, lapsedDec, lapsedLease, now.AddHours(-3));

        var (staleReq, staleDec, staleLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-200), now.AddDays(-190));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, staleReq, staleDec, staleLease, now.AddDays(-200));

        var (liveReq, liveDec, liveLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, liveReq, liveDec, liveLease, now);

        foreach (var id in new[] { lapsedLease.Id, staleLease.Id, liveLease.Id })
        {
            Assert.Equal(AccessLeaseAction.None, (await accessLeaseRepository.GetByIdAsync(id))!.Action);
        }

        var collectionIds = new[] { lapsedLease.CollectionId, staleLease.CollectionId, liveLease.CollectionId };
        var ended = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            collectionIds, now.AddDays(-90), now);

        var row = Assert.Single(ended);
        Assert.Equal(lapsedLease.Id, row.Id);
        Assert.Equal(AccessLeaseAction.None, row.Action);
        Assert.Equal(AccessLeaseStatus.Expired, AccessStatusDerivation.ComputeLeaseStatus(row.Action, row.NotAfter, now));
        Assert.Null(row.RevokedDate);

        Assert.Empty(await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            collectionIds, now.AddDays(-90), now.AddHours(-2)));
        Assert.Contains(
            await accessLeaseRepository.GetManyActiveByCollectionIdsAsync(collectionIds, now.AddHours(-2)),
            l => l.Id == lapsedLease.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_AlreadyEndedLease_EndsNothingAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Guarded on no early end yet, so a repeat revoke keeps the first revoker and decision.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var firstRevokerId = Guid.NewGuid();
        var secondRevokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        // The auto-approval already recorded one automatic decision against the request.
        var beforeRevoke = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Single(beforeRevoke!.Decisions);

        var first = BuildAuditDecision(lease, now);
        first.ApproverId = firstRevokerId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, first, now);

        var afterFirst = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(2, afterFirst!.Decisions.Count);

        var second = BuildAuditDecision(lease, now.AddMinutes(1));
        second.ApproverId = secondRevokerId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Cancelled, second, now.AddMinutes(1));

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.Revoked, persisted!.Action);
        Assert.Equal(firstRevokerId, persisted.RevokedBy);

        var afterSecond = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(2, afterSecond!.Decisions.Count);
        Assert.DoesNotContain(afterSecond.Decisions, d => d.ApproverId == secondRevokerId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_LapsedLease_EndsNothingAndAppendsNoDecision(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // A lapsed lease reads Expired; a late revoke must not restamp it Revoked or move its reported end.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddHours(-3), now.AddHours(-1));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, request, decision, lease, now.AddHours(-2));

        var auditDecision = BuildAuditDecision(lease, now);
        auditDecision.ApproverId = revokerId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, auditDecision, now);

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, persisted!.Action);
        Assert.Null(persisted.RevokedDate);
        Assert.Null(persisted.RevokedBy);

        var details = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Single(details!.Decisions);
        Assert.DoesNotContain(details.Decisions, d => d.ApproverId == revokerId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_StaleCallerRequestId_RecordsDecisionAgainstTheLeasesOwnRequest(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // The request id is read from the lease row, not the caller's possibly stale copy, so a verdict cannot land on
        // an unrelated request.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        var (otherRequest, otherDecision, _) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await accessRequestRepository.CreateAutoApprovedAsync(otherRequest, otherDecision);

        var staleLease = BuildLeaseFor(request, now);
        staleLease.Id = lease.Id;
        staleLease.AccessRequestId = otherRequest.Id;

        var auditDecision = BuildAuditDecision(lease, now);
        auditDecision.AccessRequestId = otherRequest.Id;
        auditDecision.ApproverId = revokerId;

        await accessLeaseRepository.RevokeAsync(staleLease, AccessLeaseAction.Revoked, auditDecision, now);

        var owning = await accessRequestRepository.GetDetailsByIdAsync(request.Id, now);
        Assert.Equal(2, owning!.Decisions.Count);
        Assert.Contains(owning.Decisions, d => d.ApproverId == revokerId);

        var unrelated = await accessRequestRepository.GetDetailsByIdAsync(otherRequest.Id, now);
        Assert.Single(unrelated!.Decisions);
        Assert.DoesNotContain(unrelated.Decisions, d => d.ApproverId == revokerId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_HolderEndsOwnLease_EndsAsCancelled(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Cancelled (holder-ended) and Revoked (operator-ended) round-trip as distinct actions.
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await SeedActiveLeaseAsync(accessRequestRepository, accessLeaseRepository, request, decision, lease, now);

        var auditDecision = BuildAuditDecision(lease, now);
        auditDecision.ApproverId = requesterId;
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Cancelled, auditDecision, now);

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.Cancelled, persisted!.Action);
        Assert.Equal(requesterId, persisted.RevokedBy);
        Assert.NotNull(persisted.RevokedDate);

        Assert.Null(await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now));
        Assert.Empty(await accessLeaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now));

        var ended = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { lease.CollectionId }, now.AddDays(-1), now);
        Assert.Equal(AccessLeaseAction.Cancelled, Assert.Single(ended).Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyEndedByCollectionIdsAsync_OrdersByEndDateMostRecentFirst(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        // An early-ended lease's end is its revoked date, not its creation or window.
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

        await accessLeaseRepository.RevokeAsync(
            secondLease, AccessLeaseAction.Cancelled, BuildAuditDecision(secondLease, now.AddHours(-1)), now.AddHours(-1));
        await accessLeaseRepository.RevokeAsync(
            firstLease, AccessLeaseAction.Revoked, BuildAuditDecision(firstLease, now), now);

        var ended = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { collectionId }, now.AddDays(-1), now);

        Assert.Equal(2, ended.Count);
        Assert.Equal(firstLease.Id, ended.First().Id);
        Assert.Equal(secondLease.Id, ended.Last().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByCollectionIdsAsync_NoCollectionIds_ReturnsEmpty(
        IAccessLeaseRepository accessLeaseRepository)
    {
        // Both reads short-circuit on an empty set instead of querying with an empty TVP (Dapper) or Contains (EF).
        var now = DateTime.UtcNow;

        Assert.Empty(await accessLeaseRepository.GetManyActiveByCollectionIdsAsync([], now));
        Assert.Empty(await accessLeaseRepository.GetManyEndedByCollectionIdsAsync([], now.AddDays(-1), now));
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
        AccessRequestAction action = AccessRequestAction.Approved, Guid? cipherId = null)
        => await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId ?? Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = notBefore,
            NotAfter = notAfter,
            Reason = "audit",
            Action = action,
            CreationDate = DateTime.UtcNow,
            ActionDate = action == AccessRequestAction.None ? null : DateTime.UtcNow,
        });

    // The mint time only has to fall inside the request's window, so elapsed leases can still be seeded.
    private static async Task SeedActiveLeaseAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        AccessRequest request, AccessDecision decision, AccessLease lease, DateTime mintTime)
    {
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        // Several callers seed rows a read must exclude, which would pass vacuously if the mint silently failed.
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
            Action = AccessLeaseAction.None,
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
            Action = AccessRequestAction.Approved,
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
            Action = AccessLeaseAction.None,
            NotBefore = notBefore,
            NotAfter = notAfter,
        };
        return (request, decision, lease);
    }
}
