using System.Net;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
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
    // The approval gate is read off the conditions, never evaluated. IGoverningRuleResolver documents the tier order.

    // In range for the 10.0.0.0/8 allowlists below and out of range for the 192.168.0.0/16 ones, which deny.
    private static readonly AccessSignals _signals = new()
    {
        IpAddress = IPAddress.Parse("10.0.0.5"),
        // No rule below has a time condition, so the instant only has to be fixed.
        Timestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
    };

    private const string _humanApproval = """[{"kind":"human_approval"}]""";
    private const string _admits = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
    private const string _admitsWithHumanApproval =
        """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]},{"kind":"human_approval"}]""";
    private const string _denies = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]}]""";
    private const string _unreadable = "not json";

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
        Assert.False(result.ConditionsUnreadable);
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

    // Combine gives Deny precedence, so evaluating the conditions would let Submit skip the approver.
    [Theory, BitAutoData]
    public async Task ResolveAsync_HumanApprovalWithDenyingIpAllowlist_StillRequiresHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]},{"kind":"human_approval"}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(2, result.Conditions.Count);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_HumanApprovalGate_DoesNotVaryWithTheCallersSignals(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]},{"kind":"human_approval"}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);
        var outOfRange = _signals with { IpAddress = IPAddress.Parse("192.168.1.1") };

        var admitted = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);
        var denied = await sutProvider.Sut.ResolveAsync(userId, cipherId, outOfRange);

        Assert.True(admitted!.RequiresHumanApproval);
        Assert.True(denied!.RequiresHumanApproval);
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
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
        // Flagged too, so a caller stripping the approval gate knows it is a fallback.
        Assert.True(result.ConditionsUnreadable);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_ConditionMissingItsKind_FailsSafeToHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // The polymorphic reader reports a missing discriminator as NotSupportedException, not JsonException, and it
        // still has to take the fail-safe.
        rule.Conditions = """[{"cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_ConditionWithKindLast_ParsesTheCondition(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // Property order is meaningless in JSON, so a late "kind" has to parse rather than fail safe to human approval.
        rule.Conditions = """[{"cidrs":["10.0.0.0/8"],"kind":"ip_allowlist"}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        var ip = Assert.IsType<IpAllowlistCondition>(Assert.Single(result.Conditions));
        Assert.Equal("10.0.0.0/8", Assert.Single(ip.Cidrs));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_ConditionWithNullCidrs_StillGoverns(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // "cidrs": null parses, so the condition itself has to survive the null and deny instead of throwing. A denial
        // does not require approval.
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":null}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Empty(Assert.IsType<IpAllowlistCondition>(Assert.Single(result.Conditions)).Cidrs);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_AutomaticAndHumanApproval_TheCallersSignalsPickTheRule(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The human-approval rule is older, so only the tier puts the automatic rule first.
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = _humanApproval;
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = _admits;
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));
        var outOfRange = _signals with { IpAddress = IPAddress.Parse("192.168.1.1") };

        var admitted = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);
        var fallback = await sutProvider.Sut.ResolveAsync(userId, cipherId, outOfRange);

        Assert.NotNull(admitted);
        Assert.False(admitted!.RequiresHumanApproval);
        Assert.Equal(newerRule.Id, admitted.RuleId);
        Assert.Equal(newerCollection.Id, admitted.CollectionId);
        Assert.NotNull(fallback);
        Assert.True(fallback!.RequiresHumanApproval);
        Assert.Equal(olderRule.Id, fallback.RuleId);
        Assert.Equal(olderCollection.Id, fallback.CollectionId);
    }

    [Theory]
    // The older automatic rule denies, so the next tier governs.
    [BitAutoData(_denies, _humanApproval, false)]
    [BitAutoData(_denies, _admits, false)]
    // Still governed when every rule denies, or the read gate would release the cipher to this caller.
    [BitAutoData(_denies, _denies, true)]
    // Same tier, so age decides.
    [BitAutoData(_admits, "[]", true)]
    // The newer rule's allowlist admits, but its approval gate keeps it in the approval tier.
    [BitAutoData(_humanApproval, _admitsWithHumanApproval, true)]
    // An unreadable rule's fail-safe approval could not activate, so any readable rule goes first.
    [BitAutoData(_unreadable, _humanApproval, false)]
    [BitAutoData(_unreadable, _denies, false)]
    public async Task ResolveAsync_TwoRules_TierThenAgeDecides(
        string olderConditions, string newerConditions, bool olderGoverns,
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = olderConditions;
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = newerConditions;
        (Collection collection, AccessRule rule) older = (olderCollection, olderRule), newer = (newerCollection, newerRule);
        var (winner, loser) = olderGoverns ? (older, newer) : (newer, older);
        // The expected loser goes first, so iteration order cannot pick the winner.
        SetupGovernedCollections(sutProvider, userId, cipherId, loser, winner);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(winner.rule.Id, result!.RuleId);
        Assert.Equal(winner.collection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_TieOnCreationDate_LowerRuleIdWins(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection lowerCollection, AccessRule lowerRule, Collection higherCollection, AccessRule higherRule)
    {
        // Same tier, so only the id can break the tie; the higher id goes first so iteration order cannot decide.
        var sharedCreation = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        lowerRule.Id = new Guid("00000000-0000-0000-0000-000000000001");
        lowerRule.CreationDate = sharedCreation;
        lowerRule.Conditions = _humanApproval;
        higherRule.Id = new Guid("00000000-0000-0000-0000-000000000002");
        higherRule.CreationDate = sharedCreation;
        higherRule.Conditions = _humanApproval;
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (higherCollection, higherRule), (lowerCollection, lowerRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(lowerRule.Id, result!.RuleId);
        Assert.Equal(lowerCollection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_GovernedRuleDeleted_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // GetByIdAsync is left unstubbed, so the rule reads as deleted after the collection was loaded.
        collection.AccessRuleId = rule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, collection);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_AlsoReachableThroughDeletedRuleCollection_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection deletedRuleCollection, AccessRule deletedRule, Collection governedCollection, AccessRule governingRule)
    {
        // A deleted rule's path is an escape, so the surviving rule does not take over.
        deletedRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        governingRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        governingRule.Conditions = """[{"kind":"human_approval"}]""";
        governingRule.Enabled = true;
        deletedRuleCollection.AccessRuleId = deletedRule.Id;
        governedCollection.AccessRuleId = governingRule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, deletedRuleCollection, governedCollection);
        // GetByIdAsync(deletedRule.Id) is left unstubbed, so it returns null.
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(governingRule.Id).Returns(governingRule);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_DisabledRule_NotGoverned(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);
        rule.Enabled = false;

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    // The bulk read releases this cipher in full, so the single read must not gate it. Mirrors
    // CipherLeaseGateTests.AuthorizeReadManyAsync_AlsoReachableThroughDisabledRuleCollection_NotGated.
    [Theory, BitAutoData]
    public async Task ResolveAsync_AlsoReachableThroughDisabledRuleCollection_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection disabledRuleCollection, AccessRule disabledRule, Collection governedCollection, AccessRule governingRule)
    {
        disabledRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        disabledRule.Conditions = "[]";
        governingRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        governingRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (disabledRuleCollection, disabledRule), (governedCollection, governingRule));
        disabledRule.Enabled = false;

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    // The bypassable shape IListRuleBypassableCiphersQuery warns admins about; the plain path releases it in full.
    [Theory, BitAutoData]
    public async Task ResolveAsync_AlsoReachableThroughPlainCollection_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection plainCollection, Collection governedCollection, AccessRule governingRule)
    {
        plainCollection.AccessRuleId = null;
        governedCollection.AccessRuleId = governingRule.Id;
        governingRule.Enabled = true;
        SetupReachableCollections(sutProvider, userId, cipherId, plainCollection, governedCollection);
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(governingRule.Id).Returns(governingRule);

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_CarriesTheRulesLeaseDurationBounds(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        rule.DefaultLeaseDurationSeconds = 900;
        rule.MaxLeaseDurationSeconds = 1800;
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(900, result!.DefaultLeaseDurationSeconds);
        Assert.Equal(1800, result.MaxLeaseDurationSeconds);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_RuleWithoutLeaseDurationBounds_CarriesNulls(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        rule.DefaultLeaseDurationSeconds = null;
        rule.MaxLeaseDurationSeconds = null;
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Null(result!.DefaultLeaseDurationSeconds);
        Assert.Null(result.MaxLeaseDurationSeconds);
    }

    // ResolvePinnedAsync reads the rule by id; a rule re-pointed since submit can't take over.

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_EnabledRule_ProjectsItOntoTheSuppliedCollection(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        rule.Enabled = true;
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        rule.MaxLeaseDurationSeconds = 1800;
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);

        var result = await sutProvider.Sut.ResolvePinnedAsync(rule.Id, collectionId);

        Assert.NotNull(result);
        Assert.Equal(rule.Id, result!.RuleId);
        // The collection is the request's own fact, carried in; the organization is the rule's.
        Assert.Equal(collectionId, result.CollectionId);
        Assert.Equal(rule.OrganizationId, result.OrganizationId);
        Assert.False(result.RequiresHumanApproval);
        Assert.False(result.ConditionsUnreadable);
        Assert.Equal(1800, result.MaxLeaseDurationSeconds);
        var ip = Assert.IsType<IpAllowlistCondition>(Assert.Single(result.Conditions));
        Assert.Equal("10.0.0.0/8", Assert.Single(ip.Cidrs));
    }

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_DoesNotConsultTheCallersCollections(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        rule.Enabled = true;
        rule.Conditions = "[]";
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);

        await sutProvider.Sut.ResolvePinnedAsync(rule.Id, collectionId);

        // Re-deriving reachability would re-run selection over today's rules, the drift the pin prevents.
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByUserIdCipherIdAsync(default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByManyIdsAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_DisabledRule_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        rule.Enabled = false;
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);

        Assert.Null(await sutProvider.Sut.ResolvePinnedAsync(rule.Id, collectionId));
    }

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_MissingRule_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, Guid ruleId, Guid collectionId)
    {
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(ruleId).Returns((AccessRule?)null);

        Assert.Null(await sutProvider.Sut.ResolvePinnedAsync(ruleId, collectionId));
    }

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_MalformedRule_FailsSafeAndFlagsConditionsUnreadable(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        rule.Enabled = true;
        rule.Conditions = "not json";
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);

        var result = await sutProvider.Sut.ResolvePinnedAsync(rule.Id, collectionId);

        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.True(result.ConditionsUnreadable);
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
        // An empty list reads as vacuously satisfied; the flag above prevents failing open.
        Assert.Empty(result.AutomatedConditions);
    }

    [Theory, BitAutoData]
    public async Task AutomatedConditions_StripsTheApprovalGateAndKeepsTheRest(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        rule.Enabled = true;
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]},{"kind":"human_approval"}]""";
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);

        var result = await sutProvider.Sut.ResolvePinnedAsync(rule.Id, collectionId);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Conditions.Count);
        // An approver's verdict settles the gate; the allowlist stays a standing, answerable condition.
        Assert.IsType<IpAllowlistCondition>(Assert.Single(result.AutomatedConditions));
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
        // Real engine, so selection evaluates the allowlists against _signals. Named, as the mock is registered by name.
        sutProvider.SetDependency<IAccessRuleEngine>(new AccessRuleEngine(), "ruleEngine").Create();
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
            // A governing rule must be enabled; pin it so the outcome does not depend on AutoFixture's bool sequence.
            rule.Enabled = true;
        }

        SetupReachableCollections(sutProvider, userId, cipherId, pairs.Select(p => p.collection).ToArray());

        foreach (var (_, rule) in pairs)
        {
            sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(rule.Id).Returns(rule);
        }
    }
}
