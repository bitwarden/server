using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Services;

[SutProviderCustomize]
public class AccessRuleWriteValidatorTests
{
    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ValidateAsync_EmptyName_ThrowsBadRequest(string name, AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = name;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("Name is required", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AllowsExtensionsWithoutMax_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "extendable";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = null;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("maximum extension length", ex.Message);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task ValidateAsync_AllowsExtensionsWithNonPositiveMax_ThrowsBadRequest(
        int maxExtensionDurationSeconds, AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "extendable";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = maxExtensionDurationSeconds;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("maximum extension length", ex.Message);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task ValidateAsync_NonPositiveDefaultLeaseDuration_ThrowsBadRequest(
        int defaultLeaseDurationSeconds, AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "rule";
        rule.AllowsExtensions = false;
        rule.DefaultLeaseDurationSeconds = defaultLeaseDurationSeconds;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("default lease duration must be a positive value", ex.Message);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task ValidateAsync_NonPositiveMaxLeaseDuration_ThrowsBadRequest(
        int maxLeaseDurationSeconds, AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "rule";
        rule.AllowsExtensions = false;
        rule.DefaultLeaseDurationSeconds = null;
        rule.MaxLeaseDurationSeconds = maxLeaseDurationSeconds;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("maximum lease duration must be a positive value", ex.Message);
    }

    // PM-39858's misconfiguration: a rule saved with a 1h default but a 15m cap pre-fills every request under it with
    // a duration submit then refuses. The edit form couples its two pickers; a direct API write bypassed that.
    [Theory, BitAutoData]
    public async Task ValidateAsync_DefaultLeaseDurationAboveMax_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "rule";
        rule.AllowsExtensions = false;
        rule.DefaultLeaseDurationSeconds = 3600;
        rule.MaxLeaseDurationSeconds = 900;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("cannot exceed the maximum lease duration", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DefaultLeaseDurationWithoutMax_Passes(AccessRule rule)
    {
        // An absent max is "no cap", so no default can exceed it.
        var sutProvider = SetupSutProvider(rule);
        rule.DefaultLeaseDurationSeconds = 7 * 24 * 60 * 60;
        rule.MaxLeaseDurationSeconds = null;

        var result = await sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_InvalidConditions_ThrowsBadRequestWithValidatorError(AccessRule rule)
    {
        var sutProvider = SetupSutProvider(rule);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Invalid("Unsupported condition kind"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Equal("Unsupported condition kind", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateName_ThrowsBadRequest(AccessRule rule, AccessRule sibling)
    {
        var sutProvider = SetupSutProvider(rule);
        rule.Name = "duplicate";
        sibling.OrganizationId = rule.OrganizationId;
        sibling.Name = "Duplicate";   // case-insensitive collision
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { sibling });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []));
        Assert.Contains("already exists", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateKeepingItsOwnName_IsValid(AccessRule rule)
    {
        var sutProvider = SetupSutProvider(rule);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { rule });

        var result = await sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [], rule.Id);

        Assert.Empty(result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateTakingAnotherRulesName_ThrowsBadRequest(AccessRule rule, AccessRule sibling)
    {
        var sutProvider = SetupSutProvider(rule);
        rule.Name = "taken";
        sibling.OrganizationId = rule.OrganizationId;
        sibling.Name = "taken";
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { rule, sibling });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [], rule.Id));
        Assert.Contains("already exists", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_NoCollections_SkipsCollectionLookup(AccessRule rule)
    {
        var sutProvider = SetupSutProvider(rule);

        var result = await sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, []);

        Assert.Empty(result);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .GetManyByManyIdsAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateCollectionIds_ReturnsThemDeduplicated(AccessRule rule,
        Collection collection)
    {
        var sutProvider = SetupSutProvider(rule);
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = null;
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var result = await sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule,
            [collection.Id, collection.Id]);

        Assert.Equal([collection.Id], result);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionNotFound_ThrowsBadRequest(AccessRule rule, Guid missingCollectionId)
    {
        var sutProvider = SetupSutProvider(rule);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [missingCollectionId]));
        Assert.Contains("could not be found", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CollectionInDifferentOrg_ThrowsBadRequest(AccessRule rule, Collection collection)
    {
        var sutProvider = SetupSutProvider(rule);
        collection.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [collection.Id]));
        Assert.Contains("do not belong to this organization", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_CreateWithGovernedCollection_ThrowsBadRequest(AccessRule rule,
        AccessRule otherRule, Collection collection)
    {
        var sutProvider = SetupSutProvider(rule);
        otherRule.OrganizationId = rule.OrganizationId;
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = otherRule.Id;
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [collection.Id]));
        Assert.Contains("already governed by another access rule", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateWithCollectionGovernedByAnotherRule_ThrowsBadRequest(AccessRule rule,
        AccessRule otherRule, Collection collection)
    {
        var sutProvider = SetupSutProvider(rule);
        otherRule.OrganizationId = rule.OrganizationId;
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = otherRule.Id;
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [collection.Id], rule.Id));
        Assert.Contains("already governed by another access rule", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateWithCollectionItAlreadyGoverns_IsValid(AccessRule rule,
        Collection collection)
    {
        var sutProvider = SetupSutProvider(rule);
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = rule.Id;   // already governed by the rule under update
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var result = await sutProvider.Sut.ValidateAsync(rule.OrganizationId, rule, [collection.Id], rule.Id);

        Assert.Equal([collection.Id], result);
    }

    /// <summary>
    /// Sets up a rule that passes the field-level checks, with the conditions validator and the sibling lookup
    /// stubbed to succeed, so each test only has to arrange the check it is exercising.
    /// </summary>
    private static SutProvider<AccessRuleWriteValidator> SetupSutProvider(AccessRule rule)
    {
        var sutProvider = new SutProvider<AccessRuleWriteValidator>().Create();
        rule.Name = "rule";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        // Pin the lease durations so the outcome does not depend on AutoFixture's int sequence, which is free to hand
        // out a default above the max and trip the bounds check a test is not exercising.
        rule.DefaultLeaseDurationSeconds = null;
        rule.MaxLeaseDurationSeconds = null;
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        return sutProvider;
    }
}
