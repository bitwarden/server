using Bit.Core.AdminConsole.Entities;
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
/// The union model is the whole subject here: a cipher is gated only when EVERY collection it can be
/// reached through gates, so these tests are mostly about which combinations of collection membership
/// leave a credential exposed — and which collection is then reported as the gap.
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
    /// The ungated collection is the gap, and the rule's own collection is not — it is doing its job.
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
    /// The union test spans every enabled rule in the organization, not just the one being viewed —
    /// a cipher shared with a collection ANOTHER enabled rule governs is still fully gated, and
    /// reporting it would send an admin chasing a bypass that does not exist.
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
    /// A collection governed by a DISABLED rule gates nothing, so it is a gap like any ungated one.
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
    /// A switched-off rule gates nothing at all, so nothing can bypass it. Reporting every collection
    /// it governs would make the warning pure noise the moment an admin disables a rule.
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
    /// Only ciphers the rule actually governs are assessed. An exposed credential elsewhere in the
    /// organization is not this rule's problem to report.
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
    /// A gap is reported once however many exposed ciphers share it — the admin fixes the collection,
    /// not each cipher.
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
    /// Every way in is reported, since closing only one of them fixes nothing.
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
    /// A gap is only ever taken from a cipher that is actually exposed. A fully gated cipher sharing
    /// the rule's collection must not drag its own collections into the answer.
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
                // Exposed: one gated path, one ungated.
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
    /// A rule reached with the wrong organization on the route answers "no gaps" rather than
    /// assessing it — the query is safe to call directly, not only behind the endpoint's scoping.
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
    /// A rule governing no collection governs no cipher — and the mapping read is skipped, since the
    /// answer cannot depend on it.
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
