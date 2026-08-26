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
    // Both phases of one action are stored as distinct rows: the store is append-only, so nothing is overwritten.
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

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        var outcome = events.Single(e =>
            e.Kind == AccessAuditEventKind.RequestApproved && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRequestId == requestId);
        Assert.Equal(actorId, outcome.ActorId);
        Assert.Equal(requesterId, outcome.RequesterId);
        Assert.Equal("looks good", outcome.Detail);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RequestApproved && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessRequestId == requestId);
    }

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

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        Assert.Contains(events, e => e.AccessRequestId == visibleRequestId);
        Assert.DoesNotContain(events, e => e.AccessRequestId == hiddenRequestId);
        Assert.All(events, e => Assert.Equal(organization.Id, e.OrganizationId));
    }

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

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        Assert.DoesNotContain(events, e => e.AccessRequestId == oldId);
        Assert.Contains(events, e => e.AccessRequestId == recentId);
        Assert.Contains(events, e => e.AccessRequestId == newestId);

        var ordered = events.ToList();
        Assert.Equal(ordered.OrderByDescending(e => e.OccurredAt), ordered);
    }

    // The snapshotted name survives deleting the referenced entity, which is the point of the self-contained store.
    // A read-time join would return NULL here.
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
            Id = CombGuid.Generate(),
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

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.AccessRuleId == rule.Id && e.RuleName == "audit-rule");
    }

    // Renaming the referenced entity must NOT rewrite history, which proves the name is frozen at write rather than
    // re-resolved at read.
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
            Id = CombGuid.Generate(),
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

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        Assert.Contains(events, e => e.Kind == AccessAuditEventKind.RuleCreated && e.RuleName == "original-name");
        Assert.DoesNotContain(events, e => e.RuleName == "renamed");
    }

    // The rotation columns round-trip, enums included. They are orthogonal to Kind; the rotation event kinds arrive
    // with the rotation feature.
    [DatabaseTheory, DatabaseData]
    public async Task Create_ThenRead_RoundTripsRotationContext(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var targetSystemId = Guid.NewGuid();
        var daemonId = Guid.NewGuid();
        var rotationConfigId = Guid.NewGuid();
        var rotationJobId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now)
                with
            {
                TargetSystemId = targetSystemId,
                TargetSystemName = "target-system-name",
                DaemonId = daemonId,
                DaemonName = "daemon-name",
                RotationConfigId = rotationConfigId,
                RotationJobId = rotationJobId,
                RotationSource = PamRotationSource.OnDemand,
                SyncState = PamRotationSyncState.TargetUpdated,
            });

        var events = await accessAuditEventRepository.GetManyByOrganizationIdAsync(organization.Id, now.AddDays(-1), null, 25);

        var stored = events.Single(e => e.RotationJobId == rotationJobId);
        Assert.Equal(targetSystemId, stored.TargetSystemId);
        Assert.Equal("target-system-name", stored.TargetSystemName);
        Assert.Equal(daemonId, stored.DaemonId);
        Assert.Equal("daemon-name", stored.DaemonName);
        Assert.Equal(rotationConfigId, stored.RotationConfigId);
        Assert.Equal(PamRotationSource.OnDemand, stored.RotationSource);
        Assert.Equal(PamRotationSyncState.TargetUpdated, stored.SyncState);
    }

    // The pages partition the trail exactly, with no event served twice and none skipped. All five events deliberately
    // share an OccurredAt, which is the case the Id tiebreaker exists for.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_PagesWithoutDuplicatingOrSkipping(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requestIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var requestId in requestIds)
        {
            await accessAuditEventRepository.CreateAsync(
                BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome, now)
                    with
                { AccessRequestId = requestId });
        }

        var pageSizes = new List<int>();
        var paged = new List<Guid>();
        AccessAuditEventCursor? cursor = null;
        while (true)
        {
            var page = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
                organization.Id, now.AddDays(-1), cursor, 2);
            if (page.Count == 0)
            {
                break;
            }

            pageSizes.Add(page.Count);
            paged.AddRange(page.Select(e => e.AccessRequestId!.Value));
            var last = page.Last();
            cursor = new AccessAuditEventCursor(last.OccurredAt, last.Id);
        }

        Assert.Equal(new[] { 2, 2, 1 }, pageSizes);
        Assert.Equal(requestIds.OrderBy(id => id), paged.OrderBy(id => id));
    }

    // The reason paging is keyset and not an offset. The store is append-only and read newest first, so an event
    // written between two page requests shifts an offset window down by one and re-serves a row the caller already
    // has. A cursor is anchored to a row instead of a position, so the new event simply falls outside the page.
    [DatabaseTheory, DatabaseData]
    public async Task GetManyByOrganizationId_EventAppendedBetweenPages_DoesNotReserveRows(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requestIds = new List<Guid>();

        // Distinct, descending timestamps, so the ordering is unambiguous and the only thing under test is the cursor.
        for (var i = 0; i < 4; i++)
        {
            var requestId = Guid.NewGuid();
            requestIds.Add(requestId);
            await accessAuditEventRepository.CreateAsync(
                BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome,
                        now.AddMinutes(-i))
                    with
                { AccessRequestId = requestId });
        }

        var firstPage = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), null, 2);
        var firstPageIds = firstPage.Select(e => e.AccessRequestId!.Value).ToList();

        // A PAM action emits while the caller is between pages. Under an offset this is what pushed an already-seen
        // row onto the next page.
        var appendedId = Guid.NewGuid();
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Outcome,
                    now.AddMinutes(1))
                with
            { AccessRequestId = appendedId });

        var last = firstPage.Last();
        var secondPage = await accessAuditEventRepository.GetManyByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), new AccessAuditEventCursor(last.OccurredAt, last.Id), 2);
        var secondPageIds = secondPage.Select(e => e.AccessRequestId!.Value).ToList();

        Assert.Empty(firstPageIds.Intersect(secondPageIds));
        Assert.DoesNotContain(appendedId, secondPageIds);
        Assert.Equal(requestIds, firstPageIds.Concat(secondPageIds));
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
