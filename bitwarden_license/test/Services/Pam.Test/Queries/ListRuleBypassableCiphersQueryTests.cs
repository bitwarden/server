using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

/// <summary>
/// A cipher is gated only where every reachable collection gates it; these tests cover which
/// combinations leave it exposed, and which collection is the reported gap.
/// </summary>
[SutProviderCustomize]
public class ListRuleBypassableCiphersQueryTests
{
    private static AccessRule EnabledRule(Guid id, Guid organizationId) =>
        new() { Id = id, OrganizationId = organizationId, Enabled = true, Name = "rule" };

    private static AccessRule DisabledRule(Guid id, Guid organizationId) =>
        new() { Id = id, OrganizationId = organizationId, Enabled = false, Name = "rule" };

    private static Collection GovernedCollection(Guid id, Guid organizationId, Guid? accessRuleId) =>
        new() { Id = id, OrganizationId = organizationId, AccessRuleId = accessRuleId };

    private static CollectionCipher Mapping(Guid collectionId, Guid cipherId) =>
        new() { CollectionId = collectionId, CipherId = cipherId };

    /// <summary>
    /// Wires the three reads the query composes: the rule by id, the organization's rules, and the
    /// organization's collections and cipher mappings.
    /// </summary>
    private static void Arrange(
        SutProvider<ListRuleBypassableCiphersQuery> sutProvider,
        Guid organizationId,
        AccessRuleDetails? ruleUnderTest,
        Guid ruleUnderTestId,
        IEnumerable<AccessRule> organizationRules,
        IEnumerable<Collection> collections,
        IEnumerable<CollectionCipher> mappings)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(ruleUnderTestId).Returns(ruleUnderTest);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(organizationId).Returns(organizationRules.ToList());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByOrganizationIdAsync(organizationId).Returns(collections.ToList());
        sutProvider.GetDependency<ICollectionCipherRepository>()
            .GetManyByOrganizationIdAsync(organizationId).Returns(mappings.ToList());
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_GatedCollectionOnly_ReturnsEmpty(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [GovernedCollection(gatedCollectionId, organizationId, ruleId)],
            [Mapping(gatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    /// <summary>
    /// The ungated collection is the gap; the rule's own collection is not.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_AlsoInUngatedCollection_ReportsThatCollection(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(ungatedCollectionId, organizationId, accessRuleId: null)
            ],
            [Mapping(gatedCollectionId, cipherId), Mapping(ungatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([ungatedCollectionId], result);
    }

    /// <summary>
    /// A cipher shared with a collection another enabled rule governs is still fully gated.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_SharedWithCollectionGatedByAnotherRule_ReturnsEmpty(
        Guid organizationId, Guid ruleId, Guid otherRuleId,
        Guid gatedCollectionId, Guid otherGatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        var otherRule = EnabledRule(otherRuleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule, otherRule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(otherGatedCollectionId, organizationId, otherRuleId)
            ],
            [Mapping(gatedCollectionId, cipherId), Mapping(otherGatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    /// <summary>
    /// A collection governed by a disabled rule is a gap like any ungated one.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_SharedWithCollectionGatedByDisabledRule_ReportsIt(
        Guid organizationId, Guid ruleId, Guid disabledRuleId,
        Guid gatedCollectionId, Guid disabledCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        var disabledRule = DisabledRule(disabledRuleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule, disabledRule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(disabledCollectionId, organizationId, disabledRuleId)
            ],
            [Mapping(gatedCollectionId, cipherId), Mapping(disabledCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([disabledCollectionId], result);
    }

    /// <summary>
    /// A disabled rule gates nothing, so nothing can bypass it.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_RuleDisabled_ReturnsEmpty(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = DisabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(ungatedCollectionId, organizationId, accessRuleId: null)
            ],
            [Mapping(gatedCollectionId, cipherId), Mapping(ungatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    /// <summary>
    /// Only ciphers the rule governs are assessed.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_CipherOutsideTheRule_IsNotReported(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid unrelatedCollectionId,
        Guid governedCipherId, Guid unrelatedCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(unrelatedCollectionId, organizationId, accessRuleId: null)
            ],
            [Mapping(gatedCollectionId, governedCipherId), Mapping(unrelatedCollectionId, unrelatedCipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    /// <summary>
    /// A gap is reported a single time no matter how many exposed ciphers share it.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_DeduplicatesAcrossCiphers(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId,
        Guid firstCipherId, Guid secondCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(ungatedCollectionId, organizationId, accessRuleId: null)
            ],
            [
                Mapping(gatedCollectionId, firstCipherId), Mapping(ungatedCollectionId, firstCipherId),
                Mapping(gatedCollectionId, secondCipherId), Mapping(ungatedCollectionId, secondCipherId)
            ]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([ungatedCollectionId], result);
    }

    /// <summary>
    /// Every ungated collection is reported.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_SeveralGaps_ReportsAllOfThem(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId,
        Guid firstUngatedId, Guid secondUngatedId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(firstUngatedId, organizationId, accessRuleId: null),
                GovernedCollection(secondUngatedId, organizationId, accessRuleId: null)
            ],
            [
                Mapping(gatedCollectionId, cipherId),
                Mapping(firstUngatedId, cipherId),
                Mapping(secondUngatedId, cipherId)
            ]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal(2, result.Count);
        Assert.Contains(firstUngatedId, result);
        Assert.Contains(secondUngatedId, result);
    }

    /// <summary>
    /// Gaps come only from exposed ciphers; a fully gated cipher's collections are never reported.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_IgnoresCollectionsOfProtectedCiphers(
        Guid organizationId, Guid ruleId, Guid otherRuleId,
        Guid gatedCollectionId, Guid otherGatedCollectionId, Guid ungatedCollectionId,
        Guid exposedCipherId, Guid protectedCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        var otherRule = EnabledRule(otherRuleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [rule, otherRule],
            [
                GovernedCollection(gatedCollectionId, organizationId, ruleId),
                GovernedCollection(otherGatedCollectionId, organizationId, otherRuleId),
                GovernedCollection(ungatedCollectionId, organizationId, accessRuleId: null)
            ],
            [
                Mapping(gatedCollectionId, exposedCipherId), Mapping(ungatedCollectionId, exposedCipherId),
                // Protected: both paths gated, by two different enabled rules.
                Mapping(gatedCollectionId, protectedCipherId), Mapping(otherGatedCollectionId, protectedCipherId)
            ]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([ungatedCollectionId], result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_RuleNotFound_ReturnsEmptyWithoutReadingMappings(
        Guid organizationId, Guid ruleId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(ruleId).Returns((AccessRuleDetails?)null);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    /// <summary>
    /// A rule belonging to another organization reports no gaps.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_RuleBelongsToAnotherOrganization_ReturnsEmpty(
        Guid organizationId, Guid otherOrganizationId, Guid ruleId, Guid gatedCollectionId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, otherOrganizationId);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(ruleId).Returns(AccessRuleDetails.From(rule, [gatedCollectionId]));

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }

    /// <summary>
    /// A rule governing no collection reports no gaps without reading the mappings.
    /// </summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_RuleGovernsNoCollection_ReturnsEmptyWithoutReadingMappings(
        Guid organizationId, Guid ruleId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(ruleId).Returns(AccessRuleDetails.From(rule, []));

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionCipherRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByOrganizationIdAsync(default);
    }
}
