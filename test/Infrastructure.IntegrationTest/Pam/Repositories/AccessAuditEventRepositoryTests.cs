using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessAuditEventRepositoryTests
{
    // The full lifecycle of one request projects submit -> approve -> activate -> revoke, and a revoke is NOT a
    // request denial: the Deny decision the revoke writes (against a still-Approved request) must surface as
    // LeaseRevoked, never RequestDenied. This is the projection's subtlest correctness rule.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ProjectsRequestAndLeaseLifecycle_AndRevokeIsNotADenial(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessLeaseRepository accessLeaseRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (request, decision, lease) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);
        await accessLeaseRepository.CreateFromApprovedRequestAsync(lease, now, false);
        await accessLeaseRepository.RevokeAsync(
            lease, AccessLeaseStatus.Revoked, BuildAuditDecision(lease, revokerId, "policy change", now), now);

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestSubmitted && e.AccessRequestId == request.Id && e.ActorId == requesterId);
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestApproved && e.AccessRequestId == request.Id && e.ActorId == null);
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.AccessLeaseId == lease.Id && e.ActorId == requesterId);
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.LeaseRevoked && e.AccessLeaseId == lease.Id
            && e.ActorId == revokerId && e.Detail == "policy change");

        // The discriminator: the revoke's Deny decision must not masquerade as a request denial.
        Assert.DoesNotContain(events, e =>
            e.Kind == AccessAuditEventKind.RequestDenied && e.AccessRequestId == request.Id);

        // Newest first.
        var ordered = events.ToList();
        Assert.Equal(ordered.OrderByDescending(e => e.OccurredAt), ordered);
    }

    // A genuinely denied request (resolved with a Deny decision) projects RequestDenied naming the approver; a
    // requester's own withdrawal projects RequestCancelled with no approver.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ProjectsDeniedAndCancelledRequests(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var collectionId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        var denied = await accessRequestRepository.CreateAsync(
            BuildPending(organization.Id, collectionId, now));
        await accessRequestRepository.ResolveWithDecisionAsync(
            denied, BuildHumanDecision(denied.Id, approverId, AccessDecisionVerdict.Deny, "not now", now),
            AccessRequestStatus.Denied, now);

        var cancelled = await accessRequestRepository.CreateAsync(
            BuildPending(organization.Id, collectionId, now));
        await accessRequestRepository.CancelAsync(cancelled.Id, now);

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestDenied && e.AccessRequestId == denied.Id
            && e.ActorId == approverId && e.Detail == "not now");
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestCancelled && e.AccessRequestId == cancelled.Id
            && e.ActorId == cancelled.RequesterId);
    }

    // The trail is org-wide but scoped to a single organization: an event in another org never appears.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ScopesToOrganization(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var visible = await accessRequestRepository.CreateAsync(
            BuildPending(organization.Id, Guid.NewGuid(), now));
        var hidden = await accessRequestRepository.CreateAsync(
            BuildPending(otherOrganization.Id, Guid.NewGuid(), now));

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now);

        Assert.Contains(events, e => e.AccessRequestId == visible.Id);
        Assert.DoesNotContain(events, e => e.AccessRequestId == hidden.Id);
        Assert.All(events, e => Assert.Equal(organization.Id, e.OrganizationId));
    }

    // A refused activation (no lease minted, window still live) projects LeaseActivationRejected with the requester as
    // actor -- and must not be mistaken for the expired-unactivated case, which needs the window to have lapsed.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ProjectsLeaseActivationRejected(
        IOrganizationRepository organizationRepository,
        IAccessRequestRepository accessRequestRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();

        var (request, decision, _) = BuildAutoApproved(
            organization.Id, Guid.NewGuid(), requesterId, now.AddMinutes(-5), now.AddHours(1));
        await accessRequestRepository.CreateAutoApprovedAsync(request, decision);
        await accessRequestRepository.MarkActivationRejectedAsync(request.Id, now);

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.LeaseActivationRejected
            && e.AccessRequestId == request.Id && e.ActorId == requesterId);
        Assert.DoesNotContain(events, e =>
            e.Kind == AccessAuditEventKind.RequestExpiredUnactivated && e.AccessRequestId == request.Id);
    }

    // A rule projects RuleCreated (actor = its creator) as soon as it is created -- it need NOT govern any collection,
    // because the trail is org-scoped (this is the regression case: a standalone rule used to be invisible). A later
    // edit by a different admin projects RuleUpdated naming that editor (latest-edit model).
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ProjectsRuleCreatedAndUpdated_WithoutCollectionAssociation(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var creatorId = Guid.NewGuid();
        var editorId = Guid.NewGuid();

        var rule = new AccessRule
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "audit-rule",
            Conditions = "[]",
            CreationDate = now,
            RevisionDate = now,
            LastEditedBy = creatorId,
        };
        await accessRuleRepository.CreateAsync(rule);

        var afterCreate = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now);
        Assert.Contains(afterCreate, e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.AccessRuleId == rule.Id && e.ActorId == creatorId
            && e.RuleName == "audit-rule");
        Assert.DoesNotContain(afterCreate, e =>
            e.Kind == AccessAuditEventKind.RuleUpdated && e.AccessRuleId == rule.Id);

        // A later edit by a different admin: RuleUpdated surfaces the new editor (latest-edit model).
        rule.RevisionDate = now.AddMinutes(5);
        rule.LastEditedBy = editorId;
        await accessRuleRepository.ReplaceAsync(rule);

        var afterUpdate = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now.AddMinutes(10));
        Assert.Contains(afterUpdate, e =>
            e.Kind == AccessAuditEventKind.RuleUpdated && e.AccessRuleId == rule.Id && e.ActorId == editorId
            && e.RuleName == "audit-rule");
    }

    // Soft-deleting a rule projects RuleDeleted naming the deleter; its earlier RuleCreated survives, the delete is
    // not emitted as a RuleUpdated, and the rule drops out of the normal detail read.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ProjectsRuleDeleted_PreservesHistory_AndIsNotAnUpdate(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var creatorId = Guid.NewGuid();
        var deleterId = Guid.NewGuid();

        var rule = new AccessRule
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "audit-deleted-rule",
            Conditions = "[]",
            CreationDate = now,
            RevisionDate = now,
            LastEditedBy = creatorId,
        };
        await accessRuleRepository.CreateAsync(rule);
        await accessRuleRepository.SoftDeleteAsync(rule.Id, deleterId, now.AddMinutes(5));

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-90), now.AddMinutes(10));

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RuleDeleted && e.AccessRuleId == rule.Id && e.ActorId == deleterId
            && e.RuleName == "audit-deleted-rule");
        // History survives the soft-delete: the creation event is still projected.
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.AccessRuleId == rule.Id);
        // A delete is not an edit: RevisionDate is left untouched, so no RuleUpdated is synthesized.
        Assert.DoesNotContain(events, e =>
            e.Kind == AccessAuditEventKind.RuleUpdated && e.AccessRuleId == rule.Id);

        // The deleted rule no longer surfaces in the normal detail read.
        Assert.Null(await accessRuleRepository.GetDetailsByIdAsync(rule.Id));
    }

    private static AccessRequest BuildPending(Guid organizationId, Guid collectionId, DateTime now)
        => new()
        {
            OrganizationId = organizationId,
            CollectionId = collectionId,
            CipherId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            NotBefore = now.AddHours(1),
            NotAfter = now.AddHours(2),
            Reason = "audit",
            Status = AccessRequestStatus.Pending,
            CreationDate = now,
        };

    private static AccessDecision BuildHumanDecision(
        Guid requestId, Guid approverId, AccessDecisionVerdict verdict, string comment, DateTime now)
        => new()
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = requestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = approverId,
            Verdict = verdict,
            Comment = comment,
            CreationDate = now,
        };

    private static AccessDecision BuildAuditDecision(AccessLease lease, Guid revokerId, string reason, DateTime now)
        => new()
        {
            Id = CoreHelpers.GenerateComb(),
            AccessRequestId = lease.AccessRequestId,
            DeciderKind = AccessDeciderKind.Human,
            ApproverId = revokerId,
            Verdict = AccessDecisionVerdict.Deny,
            Comment = reason,
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
