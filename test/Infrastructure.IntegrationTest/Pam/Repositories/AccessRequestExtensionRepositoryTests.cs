using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessRequestExtensionRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateApprovedExtensionAsync_ExtendsLeaseInPlaceAndRecordsRequest(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var lease = await CreateActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, requesterId, now);
        var newNotAfter = lease.NotAfter.AddHours(1);

        var outcome = await accessRequestRepository.CreateApprovedExtensionAsync(
            BuildExtension(lease, newNotAfter, now), BuildAutoDecision(now), now);

        Assert.Equal(AccessLeaseExtendOutcome.Extended, outcome);

        // The parent lease's end is pushed out in place; no new lease is minted.
        var updatedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(updatedLease);
        Assert.Equal(newNotAfter, updatedLease!.NotAfter);
        Assert.Equal(AccessLeaseStatus.Active, updatedLease.Status);

        // The extension is recorded as an approved request pointing at the parent lease.
        Assert.Equal(1, await accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id));

        // An approved extension produces no lease of its own, so it must not surface as a startable approval.
        Assert.Null(await accessRequestRepository.GetActiveApprovedByRequesterIdCipherIdAsync(
            requesterId, lease.CipherId, now));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateApprovedExtensionAsync_SecondExtension_ReturnsAlreadyExtended(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var lease = await CreateActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, requesterId, now);

        var firstNotAfter = lease.NotAfter.AddHours(1);
        Assert.Equal(AccessLeaseExtendOutcome.Extended, await accessRequestRepository.CreateApprovedExtensionAsync(
            BuildExtension(lease, firstNotAfter, now), BuildAutoDecision(now), now));

        // A lease may be extended exactly once, so a second extension is rejected and nothing is written.
        var rejected = await accessRequestRepository.CreateApprovedExtensionAsync(
            BuildExtension(lease, firstNotAfter.AddHours(1), now), BuildAutoDecision(now), now);

        Assert.Equal(AccessLeaseExtendOutcome.AlreadyExtended, rejected);
        Assert.Equal(1, await accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id));
        var updatedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(firstNotAfter, updatedLease!.NotAfter);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateApprovedExtensionAsync_LeaseNotActive_ReturnsLeaseNotActiveAndWritesNothing(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var lease = await CreateActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, requesterId, now);
        // Revoke the lease so it is no longer active.
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseStatus.Revoked, BuildHumanDecision(lease.AccessRequestId, now), now);

        var extension = BuildExtension(lease, lease.NotAfter.AddHours(1), now);
        var outcome = await accessRequestRepository.CreateApprovedExtensionAsync(
            extension, BuildAutoDecision(now), now);

        Assert.Equal(AccessLeaseExtendOutcome.LeaseNotActive, outcome);
        Assert.Equal(0, await accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id));
        Assert.Null(await accessRequestRepository.GetByIdAsync(extension.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task CountExtensionsByLeaseIdAsync_CountsOnlyThatLease(
        IOrganizationRepository organizationRepository,
        ICollectionRepository collectionRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var collection = await collectionRepository.CreateTestCollectionAsync(organization);
        var now = DateTime.UtcNow;

        var leaseA = await CreateActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, Guid.NewGuid(), now);
        var leaseB = await CreateActiveLeaseAsync(
            accessRequestRepository, accessLeaseRepository, organization.Id, collection.Id, Guid.NewGuid(), now);

        // Extend only leaseA (a lease may be extended once); the count is scoped to its own lease.
        await accessRequestRepository.CreateApprovedExtensionAsync(
            BuildExtension(leaseA, leaseA.NotAfter.AddHours(1), now), BuildAutoDecision(now), now);

        Assert.Equal(1, await accessRequestRepository.CountExtensionsByLeaseIdAsync(leaseA.Id));
        Assert.Equal(0, await accessRequestRepository.CountExtensionsByLeaseIdAsync(leaseB.Id));
    }

    private static async Task<AccessLease> CreateActiveLeaseAsync(
        IAccessRequestRepository accessRequestRepository, IAccessLeaseRepository accessLeaseRepository,
        Guid organizationId, Guid collectionId, Guid requesterId, DateTime now)
    {
        var approved = await accessRequestRepository.CreateAsync(new AccessRequest
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = Guid.NewGuid(),
            RequesterId = requesterId,
            NotBefore = now.AddMinutes(-5),
            NotAfter = now.AddHours(1),
            Reason = "audit",
            Status = AccessRequestStatus.Approved,
            CreationDate = now,
            ResolvedDate = now,
        });

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = approved.Id,
            OrganizationId = approved.OrganizationId,
            CollectionId = approved.CollectionId,
            CipherId = approved.CipherId,
            RequesterId = approved.RequesterId,
            Status = AccessLeaseStatus.Active,
            NotBefore = approved.NotBefore,
            NotAfter = approved.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));
        return lease;
    }

    private static AccessRequest BuildExtension(AccessLease lease, DateTime newNotAfter, DateTime now)
    {
        var extension = new AccessRequest
        {
            ExtensionOfLeaseId = lease.Id,
            OrganizationId = lease.OrganizationId,
            CollectionId = lease.CollectionId,
            CipherId = lease.CipherId,
            RequesterId = lease.RequesterId,
            NotBefore = lease.NotAfter,
            NotAfter = newNotAfter,
            Reason = "need more time",
            Status = AccessRequestStatus.Approved,
            CreationDate = now,
            ResolvedDate = now,
        };
        extension.SetNewId();
        return extension;
    }

    private static AccessDecision BuildAutoDecision(DateTime now)
    {
        var decision = new AccessDecision
        {
            DeciderKind = AccessDeciderKind.Automatic,
            Verdict = AccessDecisionVerdict.Approve,
            CreationDate = now,
        };
        decision.SetNewId();
        return decision;
    }

    private static AccessDecision BuildHumanDecision(Guid accessRequestId, DateTime now)
    {
        var decision = new AccessDecision
        {
            AccessRequestId = accessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = Guid.NewGuid(),
            Verdict = AccessDecisionVerdict.Deny,
            CreationDate = now,
        };
        decision.SetNewId();
        return decision;
    }
}
