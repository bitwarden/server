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
        IAccessLeaseRepository accessLeaseRepository,
        IAccessRequestRepository accessRequestRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(organization.Id, cipherId, requesterId, now, now.AddHours(1));

        await accessLeaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        var persistedRequest = await accessRequestRepository.GetByIdAsync(request.Id);
        Assert.NotNull(persistedRequest);
        Assert.Equal(AccessRequestStatus.Approved, persistedRequest!.Status);
        Assert.NotNull(persistedRequest.ResolvedDate);

        var persistedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(persistedLease);
        Assert.Equal(AccessLeaseStatus.Active, persistedLease!.Status);
        Assert.Equal(request.Id, persistedLease.AccessRequestId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_WithinWindow_ReturnsLease(
        IOrganizationRepository organizationRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await accessLeaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

        var active = await accessLeaseRepository.GetActiveByRequesterIdCipherIdAsync(requesterId, cipherId, now);

        Assert.NotNull(active);
        Assert.Equal(lease.Id, active!.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetActiveByRequesterIdCipherIdAsync_OutsideWindow_ReturnsNull(
        IOrganizationRepository organizationRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();

        // A lease whose window has already elapsed.
        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddHours(-2), now.AddHours(-1));
        await accessLeaseRepository.CreateAutoApprovedAsync(request, decision, lease, now.AddHours(-2));

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
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        // Active, in-window lease for the requester.
        var (activeReq, activeDec, activeLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await accessLeaseRepository.CreateAutoApprovedAsync(activeReq, activeDec, activeLease, now);

        // Expired lease for the same requester — must be excluded.
        var (expiredReq, expiredDec, expiredLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddHours(-2), now.AddHours(-1));
        await accessLeaseRepository.CreateAutoApprovedAsync(expiredReq, expiredDec, expiredLease, now.AddHours(-2));

        // Active lease for a different requester — must be excluded.
        var (otherReq, otherDec, otherLease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-5), now.AddHours(1));
        await accessLeaseRepository.CreateAutoApprovedAsync(otherReq, otherDec, otherLease, now);

        var result = await accessLeaseRepository.GetManyActiveByRequesterIdAsync(requesterId, now);

        Assert.Single(result);
        Assert.Equal(activeLease.Id, result.First().Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task RevokeAsync_RevokesLeaseAndRecordsAuditDecision(
        IOrganizationRepository organizationRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, cipherId, requesterId, now.AddMinutes(-5), now.AddHours(1));
        await accessLeaseRepository.CreateAutoApprovedAsync(request, decision, lease, now);

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
