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
    // CreateAsync writes an append-only before/after pair; the read collapses them to the Outcome row.
    [DatabaseTheory, DatabaseData]
    public async Task Create_ThenRead_CollapsesTheBeforeAfterPairToItsOutcome(
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

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now);

        var row = Assert.Single(events, e => e.AccessRequestId == requestId);
        Assert.Equal(AccessAuditEventPhase.Outcome, row.Phase);
        Assert.Equal(actorId, row.ActorId);
        Assert.Equal(requesterId, row.RequesterId);
        Assert.Equal("looks good", row.Detail);
    }

    // An action whose Outcome never landed collapses to its lone Attempt.
    [DatabaseTheory, DatabaseData]
    public async Task Read_AnActionWithNoOutcome_ComesBackAsItsAttempt(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var leaseId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Attempt, now)
                with
            { AccessLeaseId = leaseId });

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now);

        var row = Assert.Single(events, e => e.AccessLeaseId == leaseId);
        Assert.Equal(AccessAuditEventPhase.Attempt, row.Phase);
    }

    // A pair split across a page boundary still collapses to one row.
    [DatabaseTheory, DatabaseData]
    public async Task Read_APairSpanningAPageBoundary_StillCollapsesToOneRow(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        // Four stored rows (two pairs); a page-at-a-time caller must still see exactly two actions.
        var older = BuildEvent(organization.Id, AccessAuditEventKind.RequestSubmitted, AccessAuditEventPhase.Attempt,
            now.AddMinutes(-5)) with
        { AccessRequestId = first };
        var newer = BuildEvent(organization.Id, AccessAuditEventKind.RequestApproved, AccessAuditEventPhase.Attempt,
            now) with
        { AccessRequestId = second };
        await accessAuditEventRepository.CreateAsync(older);
        await accessAuditEventRepository.CreateAsync(older with { Phase = AccessAuditEventPhase.Outcome });
        await accessAuditEventRepository.CreateAsync(newer);
        await accessAuditEventRepository.CreateAsync(newer with { Phase = AccessAuditEventPhase.Outcome });

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, pageSize: 1);

        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(AccessAuditEventPhase.Outcome, e.Phase));
        Assert.Equal([second, first], events.Select(e => e.AccessRequestId!.Value)); // newest first
    }

    // Paging is keyed on (OccurredAt, Id), not OccurredAt alone, so a shared instant is neither dropped nor repeated.
    [DatabaseTheory, DatabaseData]
    public async Task Read_EventsSharingAnInstant_ArePagedWithoutSkippingOrRepeating(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requestIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var requestId in requestIds)
        {
            await accessAuditEventRepository.CreateAsync(
                BuildEvent(organization.Id, AccessAuditEventKind.CredentialAccessed, AccessAuditEventPhase.Outcome, now)
                    with
                { AccessRequestId = requestId });
        }

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, pageSize: 2);

        var returned = events.Select(e => e.AccessRequestId!.Value).ToList();
        Assert.Equal(requestIds.Count, returned.Count);
        Assert.Equal(requestIds.Count, returned.Distinct().Count());
        Assert.Equal(requestIds.Order(), returned.Order());
    }

    // The trail is scoped to a single organization: an event in another org never appears.
    [DatabaseTheory, DatabaseData]
    public async Task Read_ScopesToOrganization(
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

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now);

        Assert.Contains(events, e => e.AccessRequestId == visibleRequestId);
        Assert.DoesNotContain(events, e => e.AccessRequestId == hiddenRequestId);
        Assert.All(events, e => Assert.Equal(organization.Id, e.OrganizationId));
    }

    // Both bounds are inclusive and both are honoured, newest first.
    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByRange_AndOrdersNewestFirst(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var tooOldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        var newestId = Guid.NewGuid();
        var tooNewId = Guid.NewGuid();

        await CreateAtAsync(accessAuditEventRepository, organization.Id, now.AddDays(-10), tooOldId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now.AddHours(-2), recentId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now.AddMinutes(-1), newestId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now.AddHours(1), tooNewId);

        var events = await accessAuditEventRepository.GetPageByOrganizationIdAsync(organization.Id, new AccessAuditTrailFilter
        {
            Since = now.AddDays(-1),
            Until = now,
            PageSize = 50,
        });

        Assert.DoesNotContain(events, e => e.AccessRequestId == tooOldId);
        Assert.DoesNotContain(events, e => e.AccessRequestId == tooNewId);
        Assert.Equal([newestId, recentId], events.Select(e => e.AccessRequestId!.Value));
    }

    [DatabaseTheory, DatabaseData]
    public async Task Read_ReturnsNoMoreThanThePageSize(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            await CreateAtAsync(accessAuditEventRepository, organization.Id, now.AddMinutes(-i), Guid.NewGuid());
        }

        var events = await accessAuditEventRepository.GetPageByOrganizationIdAsync(organization.Id, new AccessAuditTrailFilter
        {
            Since = now.AddDays(-1),
            Until = now,
            PageSize = 3,
        });

        Assert.Equal(3, events.Count);
    }

    // Kind filter applies to the row that survived the collapse, not to either half.
    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByKind_OnTheCollapsedRowRatherThanEitherHalf(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var refusedId = Guid.NewGuid();

        var attempt = BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Attempt, now)
            with
        { AccessRequestId = refusedId };
        await accessAuditEventRepository.CreateAsync(attempt);
        await accessAuditEventRepository.CreateAsync(attempt with
        {
            Kind = AccessAuditEventKind.LeaseActivationRejected,
            Phase = AccessAuditEventPhase.Outcome,
        });

        var activated = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            kinds: [AccessAuditEventKind.LeaseActivated]);
        var rejected = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            kinds: [AccessAuditEventKind.LeaseActivationRejected]);

        Assert.DoesNotContain(activated, e => e.AccessRequestId == refusedId);
        Assert.Contains(rejected, e => e.AccessRequestId == refusedId);
    }

    // Values within one dimension are OR-ed.
    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByKind_SelectsEveryChosenKind(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var submitted = Guid.NewGuid();
        var approved = Guid.NewGuid();
        var revoked = Guid.NewGuid();

        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, submitted,
            kind: AccessAuditEventKind.RequestSubmitted);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, approved,
            kind: AccessAuditEventKind.RequestApproved);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, revoked,
            kind: AccessAuditEventKind.LeaseRevoked);

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            kinds: [AccessAuditEventKind.RequestSubmitted, AccessAuditEventKind.LeaseRevoked]);

        Assert.Equal(new[] { submitted, revoked }.Order(), events.Select(e => e.AccessRequestId!.Value).Order());
        Assert.DoesNotContain(events, e => e.AccessRequestId == approved);
    }

    // Actor selection unions the chosen identities with the automatic bucket, which has no id of its own.
    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByActor_AndUnionsTheAutomaticBucket(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var actorId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        var byActor = Guid.NewGuid();
        var byOther = Guid.NewGuid();
        var automatic = Guid.NewGuid();

        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, byActor, actorId: actorId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, byOther, actorId: otherActorId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, automatic, actorId: null);

        var actorOnly = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, actorIds: [actorId]);
        var automaticOnly = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            includeAutomatedActor: true);
        var both = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, actorIds: [actorId],
            includeAutomatedActor: true);

        Assert.Equal([byActor], actorOnly.Select(e => e.AccessRequestId!.Value));
        Assert.Equal([automatic], automaticOnly.Select(e => e.AccessRequestId!.Value));
        Assert.Equal(new[] { byActor, automatic }.Order(), both.Select(e => e.AccessRequestId!.Value).Order());
        Assert.DoesNotContain(both, e => e.AccessRequestId == byOther);
    }

    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByRequesterAndCipher(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var requesterId = Guid.NewGuid();
        var cipherId = Guid.NewGuid();
        var wanted = Guid.NewGuid();
        var wrongRequester = Guid.NewGuid();
        var wrongCipher = Guid.NewGuid();

        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, wanted,
            requesterId: requesterId, cipherId: cipherId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, wrongRequester,
            requesterId: Guid.NewGuid(), cipherId: cipherId);
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, wrongCipher,
            requesterId: requesterId, cipherId: Guid.NewGuid());

        var byRequester = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            requesterIds: [requesterId]);
        var byBoth = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            requesterIds: [requesterId], cipherIds: [cipherId]);

        Assert.DoesNotContain(byRequester, e => e.AccessRequestId == wrongRequester);
        // Dimensions are AND-ed, so naming both narrows to the row satisfying each.
        Assert.Equal([wanted], byBoth.Select(e => e.AccessRequestId!.Value));
    }

    // Item is two columns (cipher, rule) and they union rather than intersect.
    [DatabaseTheory, DatabaseData]
    public async Task Read_FiltersByItem_UnioningCiphersWithRules(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var onCipher = Guid.NewGuid();
        var onRule = Guid.NewGuid();
        var onNeither = Guid.NewGuid();

        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, onCipher, cipherId: cipherId);
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleCreated, AccessAuditEventPhase.Outcome, now) with
            { AccessRequestId = onRule, AccessRuleId = ruleId, RuleName = "Production database" });
        await CreateAtAsync(accessAuditEventRepository, organization.Id, now, onNeither);

        var byCipher = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, cipherIds: [cipherId]);
        var byRule = await ReadAllAsync(accessAuditEventRepository, organization.Id, now, ruleIds: [ruleId]);
        var byEither = await ReadAllAsync(accessAuditEventRepository, organization.Id, now,
            cipherIds: [cipherId], ruleIds: [ruleId]);

        Assert.Equal([onCipher], byCipher.Select(e => e.AccessRequestId!.Value));
        Assert.Equal([onRule], byRule.Select(e => e.AccessRequestId!.Value));
        Assert.Equal(new[] { onCipher, onRule }.Order(), byEither.Select(e => e.AccessRequestId!.Value).Order());
        Assert.DoesNotContain(byEither, e => e.AccessRequestId == onNeither);
    }

    // One row per subject in range, however many events name it.
    [DatabaseTheory, DatabaseData]
    public async Task ReadItems_ReturnsOneRowPerSubjectTheTrailNames(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var cipherId = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();

        // The same cipher twice, so a duplicate would show up as two menu options for one item.
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Outcome, now.AddMinutes(-5))
                with
            { CipherId = cipherId, CollectionId = collectionId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseRevoked, AccessAuditEventPhase.Outcome, now)
                with
            { CipherId = cipherId, CollectionId = collectionId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleCreated, AccessAuditEventPhase.Outcome, now) with
            { AccessRuleId = ruleId, RuleName = "Production database" });

        var items = await accessAuditEventRepository.GetItemsByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), now.AddMinutes(1));

        var cipher = Assert.Single(items, item => item.CipherId == cipherId);
        Assert.Equal(collectionId, cipher.CollectionId);
        Assert.Null(cipher.RuleId);
        var rule = Assert.Single(items, item => item.RuleId == ruleId);
        Assert.Equal("Production database", rule.RuleName);
        Assert.Null(rule.CipherId);
    }

    // The menu takes the most recent of a renamed rule's several snapshotted names.
    [DatabaseTheory, DatabaseData]
    public async Task ReadItems_TakesARenamedRulesMostRecentName(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var ruleId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleCreated, AccessAuditEventPhase.Outcome, now.AddHours(-2))
                with
            { AccessRuleId = ruleId, RuleName = "Production database" });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.RuleUpdated, AccessAuditEventPhase.Outcome, now) with
            { AccessRuleId = ruleId, RuleName = "Production database (paused)" });

        var items = await accessAuditEventRepository.GetItemsByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), now.AddMinutes(1));

        Assert.Equal("Production database (paused)", Assert.Single(items, i => i.RuleId == ruleId).RuleName);
    }

    // Scoped to the same range the page read uses.
    [DatabaseTheory, DatabaseData]
    public async Task ReadItems_FollowsTheRange(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var recentCipherId = Guid.NewGuid();
        var oldCipherId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Outcome, now)
                with
            { CipherId = recentCipherId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Outcome, now.AddDays(-10))
                with
            { CipherId = oldCipherId });

        var items = await accessAuditEventRepository.GetItemsByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), now.AddMinutes(1));

        Assert.Contains(items, item => item.CipherId == recentCipherId);
        Assert.DoesNotContain(items, item => item.CipherId == oldCipherId);
    }

    [DatabaseTheory, DatabaseData]
    public async Task ReadItems_ScopesToOrganization(
        IOrganizationRepository organizationRepository,
        IAccessAuditEventRepository accessAuditEventRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync();
        var now = DateTime.UtcNow;
        var visibleCipherId = Guid.NewGuid();
        var hiddenCipherId = Guid.NewGuid();

        await accessAuditEventRepository.CreateAsync(
            BuildEvent(organization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Outcome, now)
                with
            { CipherId = visibleCipherId });
        await accessAuditEventRepository.CreateAsync(
            BuildEvent(otherOrganization.Id, AccessAuditEventKind.LeaseActivated, AccessAuditEventPhase.Outcome, now)
                with
            { CipherId = hiddenCipherId });

        var items = await accessAuditEventRepository.GetItemsByOrganizationIdAsync(
            organization.Id, now.AddDays(-1), now.AddMinutes(1));

        Assert.Contains(items, item => item.CipherId == visibleCipherId);
        Assert.DoesNotContain(items, item => item.CipherId == hiddenCipherId);
    }

    // Display name is snapshotted at write time, so it survives deleting the referenced entity.
    [DatabaseTheory, DatabaseData]
    public async Task Read_SnapshotName_SurvivesEntityDeletion(
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

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now);

        Assert.Contains(events, e =>
            e.Kind == AccessAuditEventKind.RuleCreated && e.AccessRuleId == rule.Id && e.RuleName == "audit-rule");
    }

    // The event keeps the name as written; renaming the entity must not rewrite history.
    [DatabaseTheory, DatabaseData]
    public async Task Read_SnapshotName_IsNotRewrittenByRename(
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

        var events = await ReadAllAsync(accessAuditEventRepository, organization.Id, now);

        Assert.Contains(events, e => e.Kind == AccessAuditEventKind.RuleCreated && e.RuleName == "original-name");
        Assert.DoesNotContain(events, e => e.RuleName == "renamed");
    }

    /// <summary>
    /// Walks every page the way a caller does, so a test asserting on the whole trail also exercises the resume
    /// position. <paramref name="pageSize"/> is deliberately small in the paging tests, to force boundaries where a
    /// single read would have none.
    /// </summary>
    private static async Task<List<AccessAuditEvent>> ReadAllAsync(
        IAccessAuditEventRepository repository,
        Guid organizationId,
        DateTime now,
        int pageSize = 50,
        IReadOnlyCollection<AccessAuditEventKind>? kinds = null,
        IReadOnlyCollection<Guid>? actorIds = null,
        bool includeAutomatedActor = false,
        IReadOnlyCollection<Guid>? requesterIds = null,
        IReadOnlyCollection<Guid>? cipherIds = null,
        IReadOnlyCollection<Guid>? ruleIds = null)
    {
        var all = new List<AccessAuditEvent>();
        DateTime? beforeOccurredAt = null;
        Guid? beforeId = null;

        // Bounded so a resume position that failed to advance ends the test rather than hanging it.
        for (var page = 0; page < 100; page++)
        {
            var events = await repository.GetPageByOrganizationIdAsync(organizationId, new AccessAuditTrailFilter
            {
                Since = now.AddDays(-1),
                Until = now.AddMinutes(1),
                PageSize = pageSize,
                Kinds = kinds ?? [],
                ActorIds = actorIds ?? [],
                IncludeAutomatedActor = includeAutomatedActor,
                RequesterIds = requesterIds ?? [],
                CipherIds = cipherIds ?? [],
                RuleIds = ruleIds ?? [],
                BeforeOccurredAt = beforeOccurredAt,
                BeforeId = beforeId,
            });

            all.AddRange(events);
            if (events.Count < pageSize)
            {
                return all;
            }

            var last = all[^1];
            beforeOccurredAt = last.OccurredAt;
            beforeId = last.Id;
        }

        Assert.Fail("The trail did not finish paging; the resume position is not advancing.");
        return all;
    }

    private static Task CreateAtAsync(
        IAccessAuditEventRepository repository,
        Guid organizationId,
        DateTime occurredAt,
        Guid requestId,
        AccessAuditEventKind kind = AccessAuditEventKind.RequestSubmitted,
        Guid? actorId = null,
        Guid? requesterId = null,
        Guid? cipherId = null)
        => repository.CreateAsync(
            BuildEvent(organizationId, kind, AccessAuditEventPhase.Outcome, occurredAt) with
            {
                AccessRequestId = requestId,
                ActorId = actorId,
                RequesterId = requesterId,
                CipherId = cipherId,
            });

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
