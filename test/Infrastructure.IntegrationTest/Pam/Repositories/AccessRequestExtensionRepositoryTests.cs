using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessRequestExtensionRepositoryTests
{
    /// <summary>The comment the command records on the automatic Deny when the parent lease has already ended.</summary>
    private const string _leaseEndedComment = "The lease being extended has ended";

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
            BuildExtension(lease, newNotAfter, now), BuildAutoDecision(now), now, _leaseEndedComment);

        Assert.Equal(AccessLeaseExtendOutcome.Extended, outcome);

        // The parent lease's end is pushed out in place; no new lease is minted.
        var updatedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(updatedLease);
        // Timestamps round-trip within a couple of milliseconds rather than exactly: Dapper binds DateTime as
        // DbType.DateTime (3.33 ms) on the MSSQL path, and the EF providers store microseconds.
        Assert.Equal(newNotAfter, updatedLease!.NotAfter, LaxDateTimeComparer.Default);
        Assert.Equal(AccessLeaseAction.None, updatedLease.Action);

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
            BuildExtension(lease, firstNotAfter, now), BuildAutoDecision(now), now, _leaseEndedComment));

        // A lease may be extended exactly once, so a second extension is rejected and nothing is written.
        var rejected = await accessRequestRepository.CreateApprovedExtensionAsync(
            BuildExtension(lease, firstNotAfter.AddHours(1), now), BuildAutoDecision(now), now, _leaseEndedComment);

        Assert.Equal(AccessLeaseExtendOutcome.AlreadyExtended, rejected);
        Assert.Equal(1, await accessRequestRepository.CountExtensionsByLeaseIdAsync(lease.Id));
        var updatedLease = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.Equal(firstNotAfter, updatedLease!.NotAfter, LaxDateTimeComparer.Default);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateApprovedExtensionAsync_LeaseNotActive_RecordsDeniedExtensionAndLeavesLeaseAlone(
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
        await accessLeaseRepository.RevokeAsync(lease, AccessLeaseAction.Revoked, BuildHumanDecision(lease.AccessRequestId, now), now);

        var newNotAfter = lease.NotAfter.AddHours(1);
        var extension = BuildExtension(lease, newNotAfter, now);
        var outcome = await accessRequestRepository.CreateApprovedExtensionAsync(
            extension, BuildAutoDecision(now), now, _leaseEndedComment);

        Assert.Equal(AccessLeaseExtendOutcome.LeaseNotActive, outcome);

        // The refusal is recorded rather than dropped: the request exists, denied, carrying the window that was asked
        // for and an automatic verdict naming why (PM-42632).
        var denied = await accessRequestRepository.GetDetailsByIdAsync(extension.Id, now);
        Assert.NotNull(denied);
        Assert.Equal(AccessRequestStatus.Denied, denied!.Status);
        Assert.Equal(lease.Id, denied.ExtensionOfLeaseId);
        Assert.Equal(newNotAfter, denied.NotAfter, LaxDateTimeComparer.Default);
        Assert.NotNull(denied.ResolvedDate);
        var decision = Assert.Single(denied.Decisions);
        Assert.Equal(AccessDeciderKind.Automatic, decision.DeciderKind);
        Assert.Equal(AccessDecisionVerdict.Deny, decision.Verdict);
        Assert.Equal(_leaseEndedComment, decision.Comment);
        Assert.Null(decision.ApproverId);

        // Nothing was extended: the parent lease's window is untouched.
        var untouched = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(untouched);
        Assert.Equal(lease.NotAfter, untouched!.NotAfter, LaxDateTimeComparer.Default);
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
            BuildExtension(leaseA, leaseA.NotAfter.AddHours(1), now), BuildAutoDecision(now), now, _leaseEndedComment);

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
            Action = AccessRequestAction.Approved,
            CreationDate = now,
            ActionDate = now,
        });

        var lease = new AccessLease
        {
            Id = CombGuid.Generate(),
            AccessRequestId = approved.Id,
            OrganizationId = approved.OrganizationId,
            CollectionId = approved.CollectionId,
            CipherId = approved.CipherId,
            RequesterId = approved.RequesterId,
            Action = AccessLeaseAction.None,
            NotBefore = approved.NotBefore,
            NotAfter = approved.NotAfter,
            CreationDate = now,
        };
        Assert.Equal(AccessLeaseMintOutcome.Minted,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false));
        return lease;
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_ExtensionRequest_PreconditionFailedAndMintsNothing(
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
        var extension = BuildExtension(lease, newNotAfter, now);
        Assert.Equal(AccessLeaseExtendOutcome.Extended,
            await accessRequestRepository.CreateApprovedExtensionAsync(extension, BuildAutoDecision(now), now, _leaseEndedComment));

        // Inside the extension's own window, when every other precondition holds: it is Approved, owned by the
        // requester, in-window, and has produced no lease. Only ExtensionOfLeaseId refuses the mint. This is the
        // window the parent lease is extended over, so nothing else here would.
        var duringExtension = lease.NotAfter.AddMinutes(1);

        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(
                BuildLeaseFor(extension, duringExtension), duringExtension, false));

        // No second lease for the credential, and the parent is untouched.
        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(extension.Id));
        var parent = await accessLeaseRepository.GetByIdAsync(lease.Id);
        Assert.NotNull(parent);
        Assert.Equal(AccessLeaseAction.None, parent!.Action);
    }

    [DatabaseTheory, DatabaseData]
    public async Task CreateFromApprovedRequestAsync_ExtensionRequest_ParentRevoked_StillMintsNothing(
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
        var extension = BuildExtension(lease, lease.NotAfter.AddHours(1), now);
        Assert.Equal(AccessLeaseExtendOutcome.Extended,
            await accessRequestRepository.CreateApprovedExtensionAsync(extension, BuildAutoDecision(now), now, _leaseEndedComment));

        // Revoking the parent is the case that matters: it clears the single-active-lease contention that was the
        // only thing refusing this mint, so a revoked requester could otherwise re-grant themselves the rest of the
        // window. Enforcement is on here to prove the refusal is the extension predicate, not the singleton guard.
        var duringExtension = lease.NotAfter.AddMinutes(1);
        await accessLeaseRepository.RevokeAsync(
            lease, AccessLeaseAction.Revoked, BuildHumanDecision(lease.AccessRequestId, duringExtension),
            duringExtension);

        Assert.Equal(AccessLeaseMintOutcome.PreconditionFailed,
            await accessLeaseRepository.CreateFromApprovedRequestAsync(
                BuildLeaseFor(extension, duringExtension), duringExtension, true));

        Assert.Null(await accessLeaseRepository.GetByAccessRequestIdAsync(extension.Id));
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
            Action = AccessRequestAction.Approved,
            CreationDate = now,
            ActionDate = now,
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
