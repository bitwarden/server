using System.Net;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class GoverningRuleResolverTests
{
    // Selection is structural (oldest rule wins); the resolver still evaluates the chosen rule's conditions to report
    // whether it needs human approval, so the tests drive the real engine through the substitute for that step.
    private static readonly IAccessRuleEngine _engine = new AccessRuleEngine();

    // An in-range IP for the 10.0.0.0/8 allowlists below; out-of-range for the 192.168/172.16 allowlists, which
    // therefore deny. No time-of-day conditions are used, so the timestamp is arbitrary.
    private static readonly AccessSignals _signals = new()
    {
        IpAddress = IPAddress.Parse("10.0.0.5"),
        Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
    };

    [Theory, BitAutoData]
    public async Task ResolveAsync_NoReachableCollections_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(new List<CollectionCipher>());

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_CollectionWithoutAccessRule_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection)
    {
        collection.AccessRuleId = null;
        SetupReachableCollections(sutProvider, userId, cipherId, collection);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_HumanApprovalCondition_RequiresHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(collection.Id, result.CollectionId);
        Assert.Equal(collection.OrganizationId, result.OrganizationId);
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_PassingIpAllowlistCondition_DoesNotRequireHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        var ip = Assert.IsType<IpAllowlistCondition>(Assert.Single(result.Conditions));
        Assert.Equal("10.0.0.0/8", Assert.Single(ip.Cidrs));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_ConditionsContainingHumanApproval_RequiresHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]},{"kind":"human_approval"}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(2, result.Conditions.Count);
        Assert.Contains(result.Conditions, condition => condition is HumanApprovalCondition);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_EmptyConditions_DoesNotRequireHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // A conditionless rule governs the collection for audit logging but auto-approves access.
        rule.Conditions = "[]";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Empty(result.Conditions);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_MalformedRule_FailsSafeToHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = "not json";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        // An unparseable rule fails safe to human approval rather than surfacing a rule the engine cannot evaluate.
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_MultipleRules_OldestCreationDateWins(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The older rule needs human approval; the newer one would auto-grant. Oldest wins even though it is the more
        // restrictive path — the caller is routed to an approver rather than auto-granted (do not reintroduce the
        // retired least-restrictive behaviour).
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = """[{"kind":"human_approval"}]""";
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(olderCollection.Id, result.CollectionId);
        Assert.Equal(olderRule.Id, result.RuleId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_MultipleRules_OlderAutomaticWinsOverNewerHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The mirror of the previous case: here the oldest rule auto-grants and the newer one needs human approval, so
        // the caller is auto-granted. Whichever is older governs, regardless of which is more permissive.
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Equal(olderCollection.Id, result.CollectionId);
        Assert.Equal(olderRule.Id, result.RuleId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_OldestRuleFailsAutomatedConditions_StillGoverns(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The oldest rule's IP allowlist fails for this caller; a newer rule would pass. Selection is structural, so
        // the failing oldest rule still governs — the resolver never lets a newer path pre-empt it by evaluating
        // conditions. (Downstream, the auto path then surfaces the denial; that is not the resolver's concern.)
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]}]""";
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Equal(olderCollection.Id, result.CollectionId);
        Assert.Equal(olderRule.Id, result.RuleId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_TieOnCreationDate_LowerRuleIdWins(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection lowerCollection, AccessRule lowerRule, Collection higherCollection, AccessRule higherRule)
    {
        // Two rules created at the same instant: the tie breaks on rule id (lowest wins) so the choice is total and
        // stable rather than dependent on iteration order.
        var sharedCreation = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        lowerRule.Id = new Guid("00000000-0000-0000-0000-000000000001");
        lowerRule.CreationDate = sharedCreation;
        lowerRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        higherRule.Id = new Guid("00000000-0000-0000-0000-000000000002");
        higherRule.CreationDate = sharedCreation;
        higherRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (higherCollection, higherRule), (lowerCollection, lowerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(lowerRule.Id, result!.RuleId);
        Assert.Equal(lowerCollection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_OldestRuleMalformed_FailsSafeToHumanApprovalEvenWithNewerAutoPath(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The oldest rule is unparseable; a newer rule would auto-grant. Because the oldest rule governs, it fails safe
        // to human approval rather than letting the newer parseable path auto-grant around it.
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = "not json";
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(olderCollection.Id, result.CollectionId);
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_GovernedRuleDeleted_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // The collection still points at a rule id, but the rule no longer loads (deleted after the collection was
        // read). It is dropped from the candidates, leaving nothing to govern — GetByIdAsync is left unstubbed so it
        // returns null.
        collection.AccessRuleId = rule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, collection);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_OldestGovernedRuleDeleted_NextRuleGoverns(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The oldest governing rule was deleted after the collection was read, so it is skipped and the surviving
        // newer rule governs — a deleted rule stops governing even when it would otherwise have won on age.
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"human_approval"}]""";
        olderCollection.AccessRuleId = olderRule.Id;
        newerCollection.AccessRuleId = newerRule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, olderCollection, newerCollection);
        // Only the newer rule loads; GetByIdAsync(olderRule.Id) is left unstubbed so the deleted oldest returns null.
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(newerRule.Id).Returns(newerRule);
        DriveRealEngine(sutProvider);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(newerCollection.Id, result!.CollectionId);
        Assert.Equal(newerRule.Id, result.RuleId);
        Assert.True(result.RequiresHumanApproval);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_TimeOfDayCondition_ParsedAndEvaluatedThroughResolver(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // _signals' timestamp (2026-01-01 12:00 UTC) is a Thursday inside this window, so the rule auto-approves. This
        // exercises the resolver's own camelCase parse of a time_of_day condition, including the weekday-token converter.
        rule.Conditions = """[{"kind":"time_of_day","tz":"UTC","windows":[{"days":["thu"],"from":"09:00","to":"17:00"}]}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        var time = Assert.IsType<TimeOfDayCondition>(Assert.Single(result.Conditions));
        Assert.Equal("UTC", time.Tz);
        Assert.Equal(AccessWeekday.Thu, Assert.Single(Assert.Single(time.Windows).Days));
    }

    private static void SetupReachableCollections(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, params Collection[] collections)
    {
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByUserIdCipherIdAsync(userId, cipherId)
            .Returns(collections.Select(c => new CollectionCipher { CollectionId = c.Id, CipherId = cipherId }).ToList());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(collections.ToList());
    }

    private static void SetupGovernedCollection(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
        => SetupGovernedCollections(sutProvider, userId, cipherId, (collection, rule));

    private static void SetupGovernedCollections(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        params (Collection collection, AccessRule rule)[] pairs)
    {
        foreach (var (collection, rule) in pairs)
        {
            collection.AccessRuleId = rule.Id;
        }

        SetupReachableCollections(sutProvider, userId, cipherId, pairs.Select(p => p.collection).ToArray());

        foreach (var (_, rule) in pairs)
        {
            sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);
        }

        DriveRealEngine(sutProvider);
    }

    private static void DriveRealEngine(SutProvider<GoverningRuleResolver> sutProvider)
    {
        // Drive the real engine through the substitute so resolution exercises true IP/time evaluation.
        sutProvider.GetDependency<IAccessRuleEngine>()
            .Evaluate(Arg.Any<IReadOnlyList<AccessCondition>>(), Arg.Any<AccessSignals>())
            .Returns(ci => _engine.Evaluate(ci.ArgAt<IReadOnlyList<AccessCondition>>(0), ci.ArgAt<AccessSignals>(1)));
    }
}
