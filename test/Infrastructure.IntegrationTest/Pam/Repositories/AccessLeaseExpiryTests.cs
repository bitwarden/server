using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// <see cref="IAccessLeaseRepository.ExpireDueAsync"/> is set-based, so assertions scope to this test's lease ids.
/// </summary>
public class AccessLeaseExpiryTests
{
    private static readonly TimeSpan _sweepWindow = TimeSpan.FromDays(30);

    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_LeasePastNotAfter_ReturnsIt(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var lease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));

        var expired = await accessLeaseRepository.ExpireDueAsync(now, now - _sweepWindow);

        // The caller audits and triggers from the returned row alone.
        var row = Assert.Single(expired, r => r.Id == lease.Id);
        Assert.Equal(lease.OrganizationId, row.OrganizationId);
        Assert.Equal(lease.CollectionId, row.CollectionId);
        Assert.Equal(lease.CipherId, row.CipherId);
        Assert.Equal(lease.RequesterId, row.RequesterId);
        Assert.Equal(lease.NotBefore, row.NotBefore);
        Assert.Equal(lease.NotAfter, row.NotAfter);

        // The sweep only stamps SweptDate; Expired is still derived from the closed window.
        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(now, persisted!.SweptDate!.Value, new LaxDateTimeComparer(TimeSpan.FromMilliseconds(10)));
        Assert.Equal(AccessLeaseAction.None, persisted.Action);
        Assert.Null(persisted.RevokedDate);
        Assert.Null(persisted.RevokedBy);
        Assert.Equal(AccessLeaseStatus.Expired,
            AccessStatusDerivation.ComputeLeaseStatus(persisted.Action, persisted.NotAfter, now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_InWindowAndRevokedLeases_NotReturned(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var active = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddMinutes(-5), now.AddHours(1));

        // Revoked mid-window (a lapsed lease can't be) and now past it; its access-end trigger already fired.
        var revoked = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        var revokedAt = now.AddMinutes(-90);
        await accessLeaseRepository.RevokeAsync(revoked, AccessLeaseAction.Revoked, new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = revoked.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "ended for test",
            CreationDate = revokedAt,
        }, revokedAt);

        var expired = await accessLeaseRepository.ExpireDueAsync(now, now - _sweepWindow);

        Assert.DoesNotContain(expired, r => r.Id == active.Id);
        Assert.DoesNotContain(expired, r => r.Id == revoked.Id);
        Assert.Null((await accessLeaseRepository.GetByIdAsync(active.Id))!.SweptDate);
        Assert.Null((await accessLeaseRepository.GetByIdAsync(revoked.Id))!.SweptDate);
        Assert.Equal(AccessLeaseAction.None, (await accessLeaseRepository.GetByIdAsync(active.Id))!.Action);
        Assert.Equal(AccessLeaseAction.Revoked, (await accessLeaseRepository.GetByIdAsync(revoked.Id))!.Action);
    }

    // A returned lease is stamped swept in the same call, so a second run can't return it again.
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_SecondRun_DoesNotReturnAlreadySweptLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var lease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));

        var firstRun = await accessLeaseRepository.ExpireDueAsync(now, now - _sweepWindow);
        Assert.Contains(firstRun, r => r.Id == lease.Id);

        var secondRun = await accessLeaseRepository.ExpireDueAsync(now.AddMinutes(1), now - _sweepWindow);
        Assert.DoesNotContain(secondRun, r => r.Id == lease.Id);
    }

    // The stamp is the claim: overlapping sweeps split the due leases between them and never share one.
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_ConcurrentRuns_NeverReturnTheSameLease(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var leaseIds = new HashSet<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var lease = await SeedActiveLeaseAsync(
                accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
            leaseIds.Add(lease.Id);
        }

        var runs = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => accessLeaseRepository.ExpireDueAsync(now, now - _sweepWindow))));

        var returnedIds = runs.SelectMany(r => r).Select(r => r.Id).Where(leaseIds.Contains).ToList();
        Assert.Equal(leaseIds.Count, returnedIds.Count);
        Assert.Equal(leaseIds, returnedIds.ToHashSet());
    }

    // Only an opted-in, enabled config owes its access-end trigger, recorded in the sweep's own transaction.
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_OptedInEnabledConfig_RecordsPendingAccessEnd(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organization.Id,
            Name = $"target-{Guid.NewGuid()}",
            Method = PamTargetSystemMethod.Automatic,
            SealedData = "sealed-blob",
            WrappedKey = "wrapped-key",
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });
        var optedInLease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        var notOptedInLease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        var optedIn = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, optedInLease.CipherId, target.Id, now, rotateOnAccessEnd: true));
        var notOptedIn = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, notOptedInLease.CipherId, target.Id, now, rotateOnAccessEnd: false));

        var expired = await accessLeaseRepository.ExpireDueAsync(now, now - _sweepWindow);
        Assert.Contains(expired, r => r.Id == optedInLease.Id);
        Assert.Contains(expired, r => r.Id == notOptedInLease.Id);

        var pending = await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1));
        Assert.Contains(pending, c => c.Id == optedIn.Id);
        Assert.DoesNotContain(pending, c => c.Id == notOptedIn.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_LeaseEndedBeforeWindow_NotReturned(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var since = now - _sweepWindow;
        var lease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, since.AddHours(-2), since.AddHours(-1));

        var expired = await accessLeaseRepository.ExpireDueAsync(now, since);

        Assert.DoesNotContain(expired, r => r.Id == lease.Id);
    }

    private static async Task<AccessLease> SeedActiveLeaseAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        Guid organizationId, DateTime notBefore, DateTime notAfter)
    {
        var request = new AccessRequest
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
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
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = request.Id,
            OrganizationId = organizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = notBefore,
            NotAfter = notAfter,
            CreationDate = notBefore,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, notBefore, false));

        // Read the lease back, since datetime2 round-tripping may differ from the in-memory ticks.
        return (await accessLeaseRepository.GetByIdAsync(lease.Id))!;
    }

    private static PamRotationConfig BuildConfig(Guid organizationId, Guid cipherId, Guid targetSystemId, DateTime now,
        bool rotateOnAccessEnd) => new()
        {
            OrganizationId = organizationId,
            CipherId = cipherId,
            TargetSystemId = targetSystemId,
            SealedData = "sealed-blob",
            WrappedKey = "wrapped-key",
            RotateOnAccessEnd = rotateOnAccessEnd,
            Enabled = true,
            CreationDate = now,
            RevisionDate = now,
        };
}
