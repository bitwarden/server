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
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = revokerId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "policy change",
            CreationDate = now,
        };

        await accessLeaseRepository.RevokeAsync(lease, auditDecision, now);

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
        await accessLeaseRepository.RevokeAsync(revLease, BuildAuditDecision(revLease, now), now);

        // Revoked long before the window — excluded by @Since.
        var (oldReq, oldDec, oldLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddDays(-200), now.AddDays(-100));
        await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, oldReq, oldDec, oldLease, now.AddDays(-200));
        await accessLeaseRepository.RevokeAsync(oldLease, BuildAuditDecision(oldLease, now.AddDays(-150)), now.AddDays(-150));

        var result = await accessLeaseRepository.GetManyEndedByCollectionIdsAsync(
            new[] { activeLease.CollectionId, revLease.CollectionId, oldLease.CollectionId }, since);

        Assert.Single(result);
        Assert.Equal(revLease.Id, result.First().Id);
        Assert.Equal(AccessLeaseStatus.Revoked, result.First().Status);
    }

    private static AccessDecision BuildAuditDecision(AccessLease lease, DateTime now)
        => new()
        {
            Id = CoreHelpers.GenerateComb(),
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
        await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, mintTime, false);
    }

    private static AccessLease BuildLeaseFor(AccessRequest request, DateTime now)
        => new()
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

    private static (AccessRequest, AccessDecision, AccessLease) BuildAutoApproved(
        Guid organizationId, Guid cipherId, Guid requesterId, DateTime notBefore, DateTime notAfter)
    {
        var collectionId = Guid.NewGuid();
        var request = new AccessRequest
        {
            Id = CoreHelpers.GenerateComb(),
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
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
        };
        var lease = new AccessLease
        {
            Id = CoreHelpers.GenerateComb(),
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
