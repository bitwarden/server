using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Pam.Repositories;

public class AccessAuditEventRepositoryTests
{
    // An emitted event round-trips: CreateAsync writes it and GetManyByOrganizationIdAsync reads it back with its
    // kind, phase, subject ids, and detail intact. Both phases of one action (the before/after model) are stored as
    // distinct rows -- the store is append-only, so nothing is overwritten.
    [DatabaseTheory, DatabaseData]
    public async Task Create_ThenRead_RoundTripsEventWithBothPhases(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var actorId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var attempt = BuildEvent(organization.Id, AccessAuditEventKind.RequestApproved, AccessAuditEventPhase.Attempt, now)
            with
        { ActorId = actorId, RequesterId = requesterId, AccessRequestId = requestId, Detail = "looks good" };
        await accessAuditEventRepository.CreateAsync(attempt);
        await accessAuditEventRepository.CreateAsync(attempt with { Phase = AccessAuditEventPhase.Outcome });

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1));

        var outcome = events.Single(e =>
            e.Kind == AccessAuditEventKind.RequestApproved && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRequestId == requestId);
        Assert.Equal(actorId, outcome.ActorId);
        Assert.Equal(requesterId, outcome.RequesterId);
        Assert.Equal("looks good", outcome.Detail);

        // The attempt persisted as its own row (append-only before/after), it was not overwritten by the outcome.
        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestApproved && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessRequestId == requestId);
    }

    // The trail is scoped to a single organization: an event in another org never appears.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_ScopesToOrganization(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var visibleRequestId = Guid.NewGuid();
        var hiddenRequestId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now)
                with
            { AccessRequestId = visibleRequestId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(otherOrganization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now)
                with
            { AccessRequestId = hiddenRequestId });

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1));

        Assert.Contains(events, e => e.AccessRequestId == visibleRequestId);
        Assert.DoesNotContain(events, e => e.AccessRequestId == hiddenRequestId);
        Assert.All(events, e => Assert.Equal(organization.Id, e.OrganizationId));
    }

    // Only events on or after `since` are returned, newest first.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_FiltersBySince_AndOrdersNewestFirst(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        var newestId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now.AddDays(-10))
                with
            { AccessRequestId = oldId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now.AddHours(-2))
                with
            { AccessRequestId = recentId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now)
                with
            { AccessRequestId = newestId });

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1));

        Assert.DoesNotContain(events, e => e.AccessRequestId == oldId);
        Assert.Contains(events, e => e.AccessRequestId == recentId);
        Assert.Contains(events, e => e.AccessRequestId == newestId);

        var ordered = events.ToList();
        Assert.Equal(ordered.OrderByDescending(e => e.OccurredAt), ordered);
    }

    // The point of the self-contained store: the display name is snapshotted at write time, so it SURVIVES deleting
    // the referenced entity. Emit a RuleCreated for a real rule, then delete the rule -- the event still names it
    // (a read-time join would return NULL here).
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_SnapshotName_SurvivesEntityDeletion(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var rule = new AccessRule
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "audit-rule",
            Conditions = "[]",
            CreationDate = now,
            RevisionDate = now,
        };
        await accessRuleRepository.CreateAsync(rule);
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleCreated, AccessAuditEventPhase.Outcome, now)
                with
            { AccessRuleId = rule.Id, RuleName = "audit-rule" });

        await accessRuleRepository.DeleteAsync(rule);

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1));

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.AccessRuleId == rule.Id && e.RuleName == "audit-rule");
    }

    // Renaming the referenced entity must NOT rewrite history: the event keeps the name as it was when written. This is
    // the definitive proof the name is frozen at write, not re-resolved at read.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_SnapshotName_IsNotRewrittenByRename(
        IOrganizationRepository organizationRepository,
        IAccessRuleRepository accessRuleRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        var rule = new AccessRule
        {
            Id = CoreHelpers.GenerateComb(),
            OrganizationId = organization.Id,
            Name = "original-name",
            Conditions = "[]",
            CreationDate = now,
            RevisionDate = now,
        };
        await accessRuleRepository.CreateAsync(rule);
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleCreated, AccessAuditEventPhase.Outcome, now)
                with
            { AccessRuleId = rule.Id, RuleName = "original-name" });

        rule.Name = "renamed";
        rule.RevisionDate = now.AddMinutes(5);
        await accessRuleRepository.ReplaceAsync(rule);

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1));

        Assert.Contains(events, e => e.Kind == AccessAuditEventKind.RuleCreated && e.RuleName == "original-name");
        Assert.DoesNotContain(events, e => e.RuleName == "renamed");
    }

    private static AccessAuditEventData BuildEvent(
        Guid organizationId, AccessAuditEventKind kind, AccessAuditEventPhase phase, DateTime occurredAt)
        => new()
        {
            Kind = kind,
            Phase = phase,
            OccurredAt = occurredAt,
            OrganizationId = organizationId,
        };
}
