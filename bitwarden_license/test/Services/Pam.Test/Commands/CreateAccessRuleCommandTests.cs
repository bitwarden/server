using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

/// <summary>
/// The validation these commands share lives in <see cref="AccessRuleWriteValidator"/> and is covered by
/// AccessRuleWriteValidatorTests; these tests cover persistence, timestamps, and collection association wiring.
/// </summary>
[SutProviderCustomize]
public class CreateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_PersistsWithTimestampsAndValidates(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        rule.DefaultLeaseDurationSeconds = 3600;
        rule.MaxLeaseDurationSeconds = 28800;
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, []);

        Assert.Equal(_now, result.CreationDate);
        Assert.Equal(_now, result.RevisionDate);
        Assert.Equal(3600, result.DefaultLeaseDurationSeconds);
        Assert.Equal(28800, result.MaxLeaseDurationSeconds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .CreateAsync(Arg.Is<AccessRule>(r =>
                r.DefaultLeaseDurationSeconds == 3600 && r.MaxLeaseDurationSeconds == 28800));
        // A create has no existing rule to exclude from the validator's uniqueness and conflict checks.
        await sutProvider.GetDependency<IAccessRuleWriteValidator>().Received(1)
            .ValidateAsync(rule.OrganizationId, rule, Arg.Any<IEnumerable<Guid>>(), null);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithCollections_AssociatesAndReturnsThem(AccessRule rule, Collection collectionA,
        Collection collectionB)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        var collectionIds = new[] { collectionA.Id, collectionB.Id };
        SetupValidator(sutProvider, rule.OrganizationId, [.. collectionIds]);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, collectionIds);

        Assert.Equal(collectionIds, result.CollectionIds);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .SetAccessRuleAssociationsAsync(rule.OrganizationId, rule.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(collectionIds.OrderBy(x => x))),
                Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CollectionInDifferentOrg_ThrowsBadRequest(AccessRule rule, Collection collection)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Conditions = """{"kind":"human_approval"}""";
        collection.OrganizationId = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAsync(rule, new[] { collection.Id }));
        Assert.Contains("do not belong to this organization", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CollectionGovernedByAnotherRule_ThrowsBadRequest(
        AccessRule rule, AccessRule otherRule, Collection collection)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Conditions = """{"kind":"human_approval"}""";
        otherRule.OrganizationId = rule.OrganizationId;
        otherRule.Name = "other";
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = otherRule.Id;   // governed by another rule
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { otherRule });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAsync(rule, new[] { collection.Id }));
        Assert.Contains("already governed by another access rule", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_CollectionNotFound_ThrowsBadRequest(AccessRule rule, Guid missingCollectionId)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Conditions = """{"kind":"human_approval"}""";
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection>());

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.CreateAsync(rule, new[] { missingCollectionId }));
        Assert.Contains("could not be found", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_EmptyName_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "  ";

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Contains("Name is required", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InvalidRule_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Conditions = """{"kind":"bogus"}""";
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Invalid("Unsupported rule kind"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Equal("Unsupported rule kind", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_DuplicateName_ThrowsBadRequest(AccessRule rule, AccessRule existing)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "duplicate";
        rule.Conditions = """{"kind":"human_approval"}""";
        existing.OrganizationId = rule.OrganizationId;
        existing.Name = "Duplicate";   // case-insensitive collision
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule> { existing });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Contains("already exists", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_AllowsExtensionsWithoutMax_ThrowsBadRequest(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "extendable";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = null;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Contains("maximum extension length", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task CreateAsync_AllowsExtensionsWithNonPositiveMax_ThrowsBadRequest(int maxExtensionDurationSeconds, AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "extendable";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = maxExtensionDurationSeconds;

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Contains("maximum extension length", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_AllowsExtensionsWithPositiveMax_Persists(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "extendable";
        rule.Conditions = """[{"kind":"human_approval"}]""";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = 3600;
        SetupValidator(sutProvider, rule.OrganizationId, []);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, []);

        Assert.True(result.AllowsExtensions);
        Assert.Equal(3600, result.MaxExtensionDurationSeconds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .CreateAsync(Arg.Is<AccessRule>(r => r.AllowsExtensions && r.MaxExtensionDurationSeconds == 3600));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_ValidationFails_DoesNotPersist(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(Arg.Any<Guid>(), Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>())
            .ThrowsAsync(new BadRequestException("Name is required."));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAsync(rule, []));
        Assert.Equal("Name is required.", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().CreateAsync(default!);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .SetAccessRuleAssociationsAsync(default, default, default!, default!);
    }

    private static void SetupValidator(SutProvider<CreateAccessRuleCommand> sutProvider, Guid organizationId,
        List<Guid> validatedCollectionIds)
        => sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(organizationId, Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>())
            .Returns(validatedCollectionIds);

    private static SutProvider<CreateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
