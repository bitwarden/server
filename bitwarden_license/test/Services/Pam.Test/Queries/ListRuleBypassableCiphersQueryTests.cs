using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

/// <summary>
/// A cipher is gated only where every collection reaching it is gated; the ungated ones are the reported gaps.
/// </summary>
[SutProviderCustomize]
public class ListRuleBypassableCiphersQueryTests
{
    private static AccessRule EnabledRule(Guid id, Guid organizationId) =>
        new() { Id = id, OrganizationId = organizationId, Enabled = true, Name = "rule" };

    private static AccessRule DisabledRule(Guid id, Guid organizationId) =>
        new() { Id = id, OrganizationId = organizationId, Enabled = false, Name = "rule" };

    private static CollectionCipher Mapping(Guid collectionId, Guid cipherId) =>
        new() { CollectionId = collectionId, CipherId = cipherId };

    private static void Arrange(
        SutProvider<ListRuleBypassableCiphersQuery> sutProvider,
        Guid organizationId,
        AccessRuleDetails? ruleUnderTest,
        Guid ruleUnderTestId,
        IEnumerable<Guid> gatingCollectionIds,
        IEnumerable<CollectionCipher> mappings)
    {
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(ruleUnderTestId).Returns(ruleUnderTest);
        sutProvider.GetDependency<IGatingCollectionResolver>()
            .GetGatingCollectionIdsAsync(organizationId).Returns(gatingCollectionIds.ToHashSet());
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
            [gatedCollectionId],
            [Mapping(gatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_AlsoInUngatedCollection_ReportsThatCollection(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId],
            [Mapping(gatedCollectionId, cipherId), Mapping(ungatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([ungatedCollectionId], result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_SharedWithCollectionGatedByAnotherRule_ReturnsEmpty(
        Guid organizationId, Guid ruleId,
        Guid gatedCollectionId, Guid otherGatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId, otherGatedCollectionId],
            [Mapping(gatedCollectionId, cipherId), Mapping(otherGatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    /// <summary>A disabled rule gates nothing, so nothing can bypass it.</summary>
    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_RuleDisabled_ReturnsEmpty(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = DisabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [],
            [Mapping(gatedCollectionId, cipherId), Mapping(ungatedCollectionId, cipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_CipherOutsideTheRule_IsNotReported(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid unrelatedCollectionId,
        Guid governedCipherId, Guid unrelatedCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId],
            [Mapping(gatedCollectionId, governedCipherId), Mapping(unrelatedCollectionId, unrelatedCipherId)]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_DeduplicatesAcrossCiphers(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId, Guid ungatedCollectionId,
        Guid firstCipherId, Guid secondCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId],
            [
                Mapping(gatedCollectionId, firstCipherId), Mapping(ungatedCollectionId, firstCipherId),
                Mapping(gatedCollectionId, secondCipherId), Mapping(ungatedCollectionId, secondCipherId)
            ]);

        var result = await sutProvider.Sut.GetUngatedCollectionIdsAsync(organizationId, ruleId);

        Assert.Equal([ungatedCollectionId], result);
    }

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_SeveralGaps_ReportsAllOfThem(
        Guid organizationId, Guid ruleId, Guid gatedCollectionId,
        Guid firstUngatedId, Guid secondUngatedId, Guid cipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId],
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

    [Theory, BitAutoData]
    public async Task GetUngatedCollectionIdsAsync_IgnoresCollectionsOfProtectedCiphers(
        Guid organizationId, Guid ruleId,
        Guid gatedCollectionId, Guid otherGatedCollectionId, Guid ungatedCollectionId,
        Guid exposedCipherId, Guid protectedCipherId)
    {
        var sutProvider = new SutProvider<ListRuleBypassableCiphersQuery>().Create();
        var rule = EnabledRule(ruleId, organizationId);
        Arrange(sutProvider, organizationId,
            AccessRuleDetails.From(rule, [gatedCollectionId]), ruleId,
            [gatedCollectionId, otherGatedCollectionId],
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
