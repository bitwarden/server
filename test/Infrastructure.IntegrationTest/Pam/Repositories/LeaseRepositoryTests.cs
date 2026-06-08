using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class LeaseRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAutoApprovedAsync_PersistsApprovedRequestDecisionAndActiveLease(
        IOrganizationRepository organizationRepository,
        ILeaseRepository leaseRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(organization.Id, cipherId, requesterId, now, now.AddHours(1));

        await leaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        var persistedRequest = await leaseRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persistedRequest);
        Assert.Equal(LeaseRequestStatus.Approved, persistedRequest!.Status);
        Assert.NotNull(persistedRequest.ResolvedDate);

        var persistedLease = await leaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(persistedLease);
        Assert.Equal(LeaseStatus.Active, persistedLease!.Status);
        Assert.Equal(request.Id, persistedLease.LeaseRequestId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_WithinWindow_ReturnsLease(
        IOrganizationRepository organizationRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await leaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        var active = await leaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now);

        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_OutsideWindow_ReturnsNull(
        IOrganizationRepository organizationRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        // A lease whose window has already elapsed.
        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddHours(-2), now.AddHours(-1));
        await leaseRepository.CreateAutoApprovedAsync(request, decision, lease, now.AddHours(-2));

        var active = await leaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now);

        Assert.Null(active);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActivePendingByRequesterIdCipherIdAsync_ReturnsPendingRequest(
        IOrganizationRepository organizationRepository,
        ILeaseRequestRepository leaseRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var request = await leaseRequestRepository.CreateAsync(new LeaseRequest
        {
            OrganizationId = organization.Id,
            CollectionId = Guid.NewGuid(),
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = now.AddHours(1),
            NotAfter = now.AddHours(2),
            Reason = "audit",
            Status = LeaseRequestStatus.Pending,
            CreationDate = now,
        });

        var pending = await leaseRequestRepository.GetActivePendingByRequesterIdCipherIdAsync(requesterId, cipherId);

        Assert.NotNull(pending);
        Assert.Equal(request.Id, pending!.Id);
        Assert.Equal("audit", pending.Reason);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyActiveByRequesterIdAsync_ReturnsOnlyActiveLeasesInWindow(
        IOrganizationRepository organizationRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        // Active, in-window lease for the requester.
        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await leaseRepository.CreateAutoApprovedAsync(activeReq, activeDec, activeLease, now);

        // Expired lease for the same requester — must be excluded.
        var (expiredReq, expiredDec, expiredLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddHours(-2), now.AddHours(-1));
        await leaseRepository.CreateAutoApprovedAsync(expiredReq, expiredDec, expiredLease, now.AddHours(-2));

        // Active lease for a different requester — must be excluded.
        var (otherReq, otherDec, otherLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await leaseRepository.CreateAutoApprovedAsync(otherReq, otherDec, otherLease, now);

        var result = await leaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now);

        Assert.Single(result);
        Assert.Equal(activeLease.Id, result.First().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_RevokesLeaseAndRecordsAuditDecision(
        IOrganizationRepository organizationRepository,
        ILeaseRepository leaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await leaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        var auditDecision = new LeaseDecision
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = lease.LeaseRequestId,
            DeciderKind = LeaseDecisionKind.Human,
            ApproverId = revokerId,
            Decision = LeaseDecisionVerdict.Deny,
            Comment = "policy change",
            CreationDate = now,
        };

        await leaseRepository.RevokeAsync(lease, auditDecision, now);

        var persisted = await leaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(persisted);
        Assert.Equal(LeaseStatus.Revoked, persisted!.Status);
        Assert.Equal(revokerId, persisted.RevokedBy);
        Assert.NotNull(persisted.RevokedDate);
    }

    private static (LeaseRequest, LeaseDecision, Lease) BuildAutoApproved(
        Guid organizationId, Guid cipherId, Guid requesterId, DateTime notBefore, DateTime notAfter)
    {
        var collectionId = Guid.NewGuid();
        var request = new LeaseRequest
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = cipherId,
            RequesterId = requesterId,
            NotBefore = notBefore,
            NotAfter = notAfter,
            Status = LeaseRequestStatus.Approved,
        };
        var decision = new LeaseDecision
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            DeciderKind = LeaseDecisionKind.Policy,
            Decision = LeaseDecisionVerdict.Approve,
        };
        var lease = new Lease
        {
            Id = CoreHelpers.GenerateComb(),
            LeaseRequestId = request.Id,
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = cipherId,
            RequesterId = requesterId,
            Status = LeaseStatus.Active,
            NotBefore = notBefore,
            NotAfter = notAfter,
        };
        return (request, decision, lease);
    }
}
