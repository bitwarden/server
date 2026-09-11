using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// The lease natural-expiry sweep (<see cref="IAccessLeaseRepository.ExpireDueAsync"/>); a returned lease is
/// journaled so a later run can't return it again. Set-based, so assertions scope to this test's lease ids.
/// </summary>
public class AccessLeaseExpiryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_LeasePastNotAfter_ReturnsIt(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        // Minted while the window was open, but the window has since elapsed on its own.
        var lease = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));

        var expired = await accessLeaseRepository.ExpireDueAsync(now);

        // The returned row is self-contained: everything the caller audits/triggers on comes straight off the lease.
        var row = Assert.Single(expired, r => r.Id == lease.Id);
        Assert.Equal(lease.OrganizationId, row.OrganizationId);
        Assert.Equal(lease.CollectionId, row.CollectionId);
        Assert.Equal(lease.CipherId, row.CipherId);
        Assert.Equal(lease.RequesterId, row.RequesterId);
        Assert.Equal(lease.NotBefore, row.NotBefore);
        Assert.Equal(lease.NotAfter, row.NotAfter);

        // The sweep records nothing on the lease itself; Expired is derived from the closed window.
        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseAction.None, persisted!.Action);
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

        // Still inside its window: not due.
        var active = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddMinutes(-5), now.AddHours(1));

        // Past its window but already Revoked; the revoke path already fired its access-end trigger.
        var revoked = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        await accessLeaseRepository.RevokeAsync(revoked, AccessLeaseAction.Revoked, new AccessDecision
        {
            Id = CombGuid.Generate(),
            AccessRequestId = revoked.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            Comment = "ended for test",
            CreationDate = now,
        }, now);

        var expired = await accessLeaseRepository.ExpireDueAsync(now);

        Assert.DoesNotContain(expired, r => r.Id == active.Id);
        Assert.DoesNotContain(expired, r => r.Id == revoked.Id);
        Assert.Equal(AccessLeaseAction.None, (await accessLeaseRepository.GetByIdAsync(active.Id))!.Action);
        Assert.Equal(AccessLeaseAction.Revoked, (await accessLeaseRepository.GetByIdAsync(revoked.Id))!.Action);
    }

    // A returned lease is journaled in the same call, so a second run can't return it again.
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

        var firstRun = await accessLeaseRepository.ExpireDueAsync(now);
        Assert.Contains(firstRun, r => r.Id == lease.Id);

        var secondRun = await accessLeaseRepository.ExpireDueAsync(now.AddMinutes(1));
        Assert.DoesNotContain(secondRun, r => r.Id == lease.Id);
    }

    // Seeds an active lease as production does: record the request, then mint by activating within its window.
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
}
