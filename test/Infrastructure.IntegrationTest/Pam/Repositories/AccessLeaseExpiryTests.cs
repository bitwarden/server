using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

/// <summary>
/// The lease natural-expiry sweep (<c>AccessLease_ExpireDue</c> via
/// <see cref="IAccessLeaseRepository.ExpireDueAsync"/>): Active leases whose window closed on its own flip to
/// Expired and are returned for the LeaseExpired audit emission / rotation access-end trigger. The sweep is
/// set-based across the whole table, so assertions scope to this test's lease ids rather than the full result.
/// </summary>
public class AccessLeaseExpiryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_ActiveLeasePastNotAfter_ExpiresAndReturnsIt(
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

        var persisted = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(AccessLeaseStatus.Expired, persisted!.Status);
        // Natural expiry involves no revoker: the revoke fields stay untouched.
        Assert.Null(persisted.RevokedDate);
        Assert.Null(persisted.RevokedBy);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_InWindowAndRevokedLeases_Untouched(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        // Still inside its window: not due.
        var active = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddMinutes(-5), now.AddHours(1));

        // Past its window but already Revoked: the sweep only flips Active leases -- an operator-ended lease must
        // never be rewritten to Expired.
        var revoked = await SeedActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, now.AddHours(-2), now.AddHours(-1));
        await accessLeaseRepository.RevokeAsync(revoked, AccessLeaseStatus.Revoked, new AccessDecision
        {
            Id = CoreHelpers.GenerateComb(),
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
        Assert.Equal(AccessLeaseStatus.Active, (await accessLeaseRepository.GetByIdAsync(active.Id))!.Status);
        Assert.Equal(AccessLeaseStatus.Revoked, (await accessLeaseRepository.GetByIdAsync(revoked.Id))!.Status);
    }

    // The sweep is idempotent: a lease it already flipped is no longer Active, so a second run never returns it
    // again -- the LeaseExpired audit event and the rotation access-end trigger fire exactly once per lease.
    [DatabaseTheory, DatabaseData]
    public async Task ExpireDueAsync_SecondRun_DoesNotReturnAlreadyExpiredLease(
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
        Assert.Equal(AccessLeaseStatus.Expired, (await accessLeaseRepository.GetByIdAsync(lease.Id))!.Status);
    }

    // Seeds an active lease the way production does: record the auto-approved request, then mint the lease by
    // activating it at a time inside the request's window (which can be in the past, so already-elapsed windows can
    // still be seeded).
    private static async Task<AccessLease> SeedActiveLeaseAsync(
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        Guid organizationId, DateTime notBefore, DateTime notAfter)
    {
        var request = new AccessRequest
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            CollectionId = Guid.NewGuid(),
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
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
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);

        var lease = new AccessLease
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = request.Id,
            OrganizationId = organizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = notBefore,
            NotAfter = notAfter,
            CreationDate = notBefore,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, notBefore, false));

        // The mint copies the persisted request's window, whose datetime2 roundtrip may differ from the in-memory
        // ticks -- read the lease back so the expiry assertions compare against what is actually stored.
        return (await accessLeaseRepository.GetByIdAsync(lease.Id))!;
    }
}
