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
    // Resolution is structural throughout: the oldest rule wins, and its human-approval gate is read off its
    // conditions. Nothing here evaluates them, so the tests assert on the rule the resolver picks and the gate it
    // reports, never on a verdict.

    // An in-range IP for the 10.0.0.0/8 allowlists below; out-of-range for the 192.168/172.16 allowlists, which
    // therefore deny.
    private static readonly AccessSignals _signals = new()
    {
        IpAddress = IPAddress.Parse("10.0.0.5"),
        // Fixed instant: no rule below carries a time-of-day condition, so it only has to be deterministic.
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

    // PM-42256: the gate used to be read off the engine's verdict, and Combine gives deny precedence over
    // requires-approval, so a denying condition alongside the human-approval one folded the rule to Deny and reported
    // "no approval needed". Submit then took the automatic path and refused the request outright, and the pre-check
    // advertised Automatic, so the caller never reached the approver whose decision the rule exists to require.
    [Theory, BitAutoData]
    public async Task ResolveAsync_HumanApprovalWithDenyingIpAllowlist_StillRequiresHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // 192.168.0.0/16 does not contain the caller's 10.0.0.5, so this allowlist denies.
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
        // The same rule has to resolve the same way for a caller its allowlist admits and one it denies: the gate is a
        // property of the rule, not of who is asking or from where.
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
        // An unparseable rule fails safe to human approval rather than surfacing a rule the engine cannot evaluate.
        Assert.IsType<HumanApprovalCondition>(Assert.Single(result.Conditions));
        // Flagged as well as substituted: the stand-in is indistinguishable from a genuine [human_approval] rule, and
        // a caller that strips the approval gate before evaluating needs to know it is looking at a fallback.
        Assert.True(result.ConditionsUnreadable);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_ConditionMissingItsKind_FailsSafeToHumanApproval(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // A stored condition with no discriminator cannot be mapped to a kind, and the polymorphic reader reports that
        // as NotSupportedException rather than JsonException. Unless both are caught it escapes ResolveAsync instead of
        // taking the fail-safe below, so a document the server cannot interpret would surface as an unhandled
        // exception rather than routing to an approver.
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
        // Property order is meaningless in JSON, so a stored document that writes "kind" after the properties it
        // discriminates has to read back as the condition it names — not fail safe to human approval, which would
        // route a caller the allowlist auto-approves to an approver instead.
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
        // "cidrs": null parses, so this never reaches Parse's fail-safe: the condition itself has to survive the null
        // and deny, or the NullReferenceException escapes from inside the engine. The rule still governs — an
        // allowlist that matches nothing denies, which the auto path surfaces downstream, and a denial is not the
        // same thing as requiring approval.
        rule.Conditions = """[{"kind":"ip_allowlist","cidrs":null}]""";
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Empty(Assert.IsType<IpAllowlistCondition>(Assert.Single(result.Conditions)).Cidrs);
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
        newerRule.Enabled = true;
        olderCollection.AccessRuleId = olderRule.Id;
        newerCollection.AccessRuleId = newerRule.Id;
        SetupReachableCollections(sutProvider, userId, cipherId, olderCollection, newerCollection);
        // Only the newer rule loads; GetByIdAsync(olderRule.Id) is left unstubbed so the deleted oldest returns null.
        sutProvider.GetDependency<IAccessRuleRepository>().GetByIdAsync(newerRule.Id).Returns(newerRule);

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(newerCollection.Id, result!.CollectionId);
        Assert.Equal(newerRule.Id, result.RuleId);
        Assert.True(result.RequiresHumanApproval);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_DisabledRule_NotGoverned(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId, Collection collection, AccessRule rule)
    {
        // A disabled rule is inactive and does not gate access, so a cipher reached only through it is ungoverned.
        SetupGovernedCollection(sutProvider, userId, cipherId, collection, rule);
        rule.Enabled = false;

        Assert.Null(await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals));
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_OldestRuleDisabled_NewerEnabledRuleGoverns(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection olderCollection, AccessRule olderRule, Collection newerCollection, AccessRule newerRule)
    {
        // The oldest rule is disabled and auto-granting; the newer rule is enabled and needs human approval. A disabled
        // rule must not shadow a newer active one, so the newer rule governs — access is not silently auto-granted.
        olderRule.CreationDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        olderRule.Conditions = "[]";
        newerRule.CreationDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        newerRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (olderCollection, olderRule), (newerCollection, newerRule));
        olderRule.Enabled = false;

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.Equal(newerCollection.Id, result!.CollectionId);
        Assert.Equal(newerRule.Id, result.RuleId);
        Assert.True(result.RequiresHumanApproval);
    }

    // PM-39858: the resolved rule is what submit and the pre-check both read, so it has to carry the rule's lease
    // duration bounds. It previously copied only the extension fields, leaving the two lease-duration ones unreadable
    // downstream.
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

    // ResolvePinnedAsync answers a different question from ResolveAsync: not "which rule governs this caller now" but
    // "which rule decided this request". It therefore reads the rule straight off its id and never consults the
    // caller's collections, so a rule created or re-pointed since submit cannot take over.

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

        // Re-deriving reachability would reintroduce oldest-wins over today's rules, which is the drift the pin exists
        // to prevent.
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByUserIdCipherIdAsync(default, default);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByManyIdsAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ResolvePinnedAsync_DisabledRule_ReturnsNull(
        SutProvider<GoverningRuleResolver> sutProvider, AccessRule rule, Guid collectionId)
    {
        // Dropped for the same reason ResolveAsync drops it: an admin has switched the rule off, so it no longer gates.
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
        // Stripping the fail-safe gate leaves nothing, which the engine reads as vacuously satisfied -- the flag above
        // is what stops a caller evaluating this list from failing open.
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
        // An approver's verdict settles the gate once; the allowlist is a standing condition and stays answerable.
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
