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
/// The pending access end a lease end records for the rotation sweep. The sweep read is global, so assertions scope to
/// this test's config ids. The expiry side lives in <see cref="AccessLeaseExpiryTests"/>, whose tests must not run
/// beside another class's expiry sweep.
/// </summary>
public class PamPendingAccessEndTests
{
    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_OptedInEnabledConfig_RecordsPendingAccessEnd(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
            config.CipherId, now.AddMinutes(-5), now.AddHours(1));

        await RevokeAsync(accessLeaseRepository, lease, now);

        var pending = await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1));
        Assert.Single(pending, c => c.Id == config.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_ConfigNotOptedInOrPaused_RecordsNothing(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var notOptedIn = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, target.Id, now, rotateOnAccessEnd: false));
        var paused = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, target.Id, now, enabled: false));

        foreach (var config in new[] { notOptedIn, paused })
        {
            var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
                config.CipherId, now.AddMinutes(-5), now.AddHours(1));
            await RevokeAsync(accessLeaseRepository, lease, now);
        }

        var pending = await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1));
        Assert.DoesNotContain(pending, c => c.Id == notOptedIn.Id || c.Id == paused.Id);
    }

    // A repeat revoke ends nothing, so it must not record a second row for the same lease.
    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_Repeated_RecordsNothingMore(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
            config.CipherId, now.AddMinutes(-5), now.AddHours(1));

        await RevokeAsync(accessLeaseRepository, lease, now);
        await pamRotationConfigRepository.DeletePendingAccessEndAsync(config.Id);
        await RevokeAsync(accessLeaseRepository, lease, now.AddMinutes(1));

        var pending = await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddMinutes(2));
        Assert.DoesNotContain(pending, c => c.Id == config.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyPendingAccessEndAsync_LeavesNewerRowsToTheInlineTrigger(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
            config.CipherId, now.AddMinutes(-5), now.AddHours(1));

        await RevokeAsync(accessLeaseRepository, lease, now);

        Assert.DoesNotContain(await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddMinutes(-1)),
            c => c.Id == config.Id);
        Assert.Contains(await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1)),
            c => c.Id == config.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task DeletePendingAccessEndAsync_ClearsEveryLeaseOfTheConfig(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var config = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        var other = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        foreach (var cipherId in new[] { config.CipherId, config.CipherId, other.CipherId })
        {
            var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
                cipherId, now.AddMinutes(-5), now.AddHours(1));
            await RevokeAsync(accessLeaseRepository, lease, now);
        }

        await pamRotationConfigRepository.DeletePendingAccessEndAsync(config.Id);

        var pending = await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1));
        Assert.DoesNotContain(pending, c => c.Id == config.Id);
        Assert.Single(pending, c => c.Id == other.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RaiseManualObligationOnAccessEndAsync_RaisesOnceAndClearsPending(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Manual);
        var config = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, target.Id, now, nextRotationDate: now.AddDays(7)));
        var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
            config.CipherId, now.AddMinutes(-5), now.AddHours(1));
        await RevokeAsync(accessLeaseRepository, lease, now);
        var raisedAt = now.AddMinutes(1);

        Assert.True(await pamRotationConfigRepository.RaiseManualObligationOnAccessEndAsync(config.Id, raisedAt));
        Assert.False(await pamRotationConfigRepository.RaiseManualObligationOnAccessEndAsync(config.Id, raisedAt));

        var persisted = await pamRotationConfigRepository.GetByIdAsync(config.Id);
        Assert.Equal(raisedAt, persisted!.NextRotationDate!.Value, LaxDateTimeComparer.Default);
        Assert.Equal(raisedAt, persisted.RevisionDate, LaxDateTimeComparer.Default);
        Assert.DoesNotContain(await pamRotationConfigRepository.GetManyPendingAccessEndAsync(raisedAt),
            c => c.Id == config.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RaiseManualObligationOnAccessEndAsync_NothingPending_ChangesNothing(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Manual);
        var config = await pamRotationConfigRepository.CreateAsync(
            BuildConfig(organization.Id, target.Id, now, nextRotationDate: now.AddDays(7)));

        Assert.False(await pamRotationConfigRepository.RaiseManualObligationOnAccessEndAsync(config.Id, now));

        var persisted = await pamRotationConfigRepository.GetByIdAsync(config.Id);
        Assert.Equal(now.AddDays(7), persisted!.NextRotationDate!.Value, LaxDateTimeComparer.Default);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ConfigAndOrganizationDelete_WithPendingAccessEnd_Cascade(
        IOrganizationRepository organizationRepository,
        IPamTargetSystemRepository pamTargetSystemRepository,
        IPamRotationConfigRepository pamRotationConfigRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var target = await CreateTargetAsync(pamTargetSystemRepository, organization.Id, now,
            PamTargetSystemMethod.Automatic);
        var deleted = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        var kept = await pamRotationConfigRepository.CreateAsync(BuildConfig(organization.Id, target.Id, now));
        foreach (var cipherId in new[] { deleted.CipherId, kept.CipherId })
        {
            var lease = await SeedLeaseAsync(accessRequestRepository, accessLeaseRepository, organization.Id,
                cipherId, now.AddMinutes(-5), now.AddHours(1));
            await RevokeAsync(accessLeaseRepository, lease, now);
        }

        Assert.True(await pamRotationConfigRepository.DeleteWithJobsAsync(deleted.Id));
        await organizationRepository.DeleteAsync(organization);

        Assert.Null(await pamRotationConfigRepository.GetByIdAsync(kept.Id));
        Assert.DoesNotContain(await pamRotationConfigRepository.GetManyPendingAccessEndAsync(now.AddSeconds(1)),
            c => c.OrganizationId == organization.Id);
    }

    private static Task RevokeAsync(IAccessLeaseRepository accessLeaseRepository, AccessLease lease, DateTime now)
        => accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        }, now);

    private static async Task<PamTargetSystem> CreateTargetAsync(
        IPamTargetSystemRepository pamTargetSystemRepository, Guid organizationId, DateTime now,
        PamTargetSystemMethod method)
        => await pamTargetSystemRepository.CreateAsync(new PamTargetSystem
        {
            OrganizationId = organizationId,
            Name = $"target-{Guid.NewGuid()}",
            Method = method,
            SealedData = "sealed-blob",
            WrappedKey = "wrapped-key",
            Status = PamTargetSystemStatus.Active,
            CreationDate = now,
            RevisionDate = now,
        });

    private static PamRotationConfig BuildConfig(Guid organizationId, Guid targetSystemId, DateTime now,
        bool rotateOnAccessEnd = true, bool enabled = true, DateTime? nextRotationDate = null) => new()
        {
            OrganizationId = organizationId,
            CipherId = Guid.NewGuid(),
            TargetSystemId = targetSystemId,
            SealedData = "sealed-blob",
            WrappedKey = "wrapped-key",
            RotateOnAccessEnd = rotateOnAccessEnd,
            NextRotationDate = nextRotationDate,
            Enabled = enabled,
            CreationDate = now,
            RevisionDate = now,
        };

    private static async Task<AccessLease> SeedLeaseAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        Guid organizationId, Guid cipherId, DateTime notBefore, DateTime notAfter)
    {
        var request = new AccessRequest
        {
            Id = CombGuid.Generate(),
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId,
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
            CipherId = cipherId,
            RequesterId = request.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = notBefore,
            NotAfter = notAfter,
            CreationDate = notBefore,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, notBefore, false));

        return (await accessLeaseRepository.GetByIdAsync(lease.Id))!;
    }
}
