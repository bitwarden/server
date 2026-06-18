using System.Net;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Engine;
using Bit.Pam.Entities;
using Bit.Pam.Models.Conditions;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Services;

[SutProviderCustomize]
public class GoverningRuleResolverTests
{
    // The resolver now evaluates each candidate rule, so the tests drive the real engine through the substitute.
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
    public async Task ResolveAsync_AutomaticGrantPath_BeatsHumanApprovalPath(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection automaticCollection, AccessRule automaticRule, Collection humanCollection, AccessRule humanRule)
    {
        // One reachable collection auto-grants (passing IP allowlist); the other needs human approval.
        automaticRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        humanRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (automaticCollection, automaticRule), (humanCollection, humanRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        // Least-restrictive wins: the caller is never routed to an approver when some path would auto-grant.
        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Equal(automaticCollection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_FailingAutomaticRule_DoesNotPreemptGrantingHumanApprovalPath(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection automaticCollection, AccessRule automaticRule, Collection humanCollection, AccessRule humanRule)
    {
        // The automatic rule's IP allowlist fails for this caller; the human-approval rule would still grant.
        automaticRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]}]""";
        humanRule.Conditions = """[{"kind":"human_approval"}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (automaticCollection, automaticRule), (humanCollection, humanRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        // A failing automatic rule must not pre-empt a path that needs approval but would grant.
        Assert.NotNull(result);
        Assert.True(result!.RequiresHumanApproval);
        Assert.Equal(humanCollection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_NoRulePasses_ResolvesToAutoDenyPath(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection firstCollection, AccessRule firstRule, Collection secondCollection, AccessRule secondRule)
    {
        // Neither automatic rule's allowlist matches the caller, and no approval path exists, so every path denies.
        firstRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]}]""";
        secondRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["172.16.0.0/12"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (firstCollection, firstRule), (secondCollection, secondRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        // Falls through to a deny path (not routed to a human); the auto path then surfaces the denial downstream.
        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_TwoAutomaticRules_ResolvesToThePassingOne(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection failingCollection, AccessRule failingRule, Collection passingCollection, AccessRule passingRule)
    {
        failingRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["192.168.0.0/16"]}]""";
        passingRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (failingCollection, failingRule), (passingCollection, passingRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Equal(passingCollection.Id, result.CollectionId);
    }

    [Theory, BitAutoData]
    public async Task ResolveAsync_MalformedRuleAlongsidePassingAutomatic_AutoGrants(
        SutProvider<GoverningRuleResolver> sutProvider, Guid userId, Guid cipherId,
        Collection malformedCollection, AccessRule malformedRule, Collection automaticCollection, AccessRule automaticRule)
    {
        // The malformed rule fails safe to human approval for its own path, but a different parseable rule auto-grants,
        // so the union/OR auto-grant path wins.
        malformedRule.Conditions = "not json";
        automaticRule.Conditions = """[{"kind":"ip_allowlist","cidrs":["10.0.0.0/8"]}]""";
        SetupGovernedCollections(sutProvider, userId, cipherId,
            (malformedCollection, malformedRule), (automaticCollection, automaticRule));

        var result = await sutProvider.Sut.ResolveAsync(userId, cipherId, _signals);

        Assert.NotNull(result);
        Assert.False(result!.RequiresHumanApproval);
        Assert.Equal(automaticCollection.Id, result.CollectionId);
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

        // Drive the real engine through the substitute so resolution exercises true IP/time evaluation.
        sutProvider.GetDependency<IAccessRuleEngine>()
            .Evaluate(Arg.Any<IReadOnlyList<AccessCondition>>(), Arg.Any<AccessSignals>())
            .Returns(ci => _engine.Evaluate(ci.ArgAt<IReadOnlyList<AccessCondition>>(0), ci.ArgAt<AccessSignals>(1)));
    }
}
