using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Commercial.Pam.OrganizationFeatures.Commands;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Commands;

[SutProviderCustomize]
public class CreateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CreateAsync_HappyPath_PersistsWithTimestampsAndValidates(AccessRule rule)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """{"kind":"human_approval"}""";
        rule.DefaultLeaseDurationSeconds = 3600;
        rule.MaxLeaseDurationSeconds = 28800;
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
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
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_WithCollections_AssociatesAndReturnsThem(AccessRule rule, Collection collectionA,
        Collection collectionB)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "VPN + business hours";
        rule.Conditions = """{"kind":"human_approval"}""";
        collectionA.OrganizationId = rule.OrganizationId;
        collectionA.AccessRuleId = null;
        collectionB.OrganizationId = rule.OrganizationId;
        collectionB.AccessRuleId = null;
        var collectionIds = new[] { collectionA.Id, collectionB.Id };
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(collectionIds.OrderBy(x => x))))
            .Returns(new List<Collection> { collectionA, collectionB });

        await sutProvider.Sut.CreateAsync(rule, collectionIds);

        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .SetCollectionAssociationsAsync(rule.OrganizationId, rule.Id,
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
    public async Task CreateAsync_CollectionGovernedByAnotherRule_ThrowsBadRequest(AccessRule rule, Collection collection)
    {
        var sutProvider = SetupSutProvider();
        rule.Name = "test";
        rule.Conditions = """{"kind":"human_approval"}""";
        collection.OrganizationId = rule.OrganizationId;
        collection.AccessRuleId = Guid.NewGuid();
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
        rule.Conditions = """{"kind":"human_approval"}""";
        rule.AllowsExtensions = true;
        rule.MaxExtensionDurationSeconds = 3600;
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(rule.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(rule.OrganizationId)
            .Returns(new List<AccessRule>());
        sutProvider.GetDependency<IAccessRuleRepository>()
            .CreateAsync(rule)
            .Returns(rule);

        var result = await sutProvider.Sut.CreateAsync(rule, []);

        Assert.True(result.AllowsExtensions);
        Assert.Equal(3600, result.MaxExtensionDurationSeconds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .CreateAsync(Arg.Is<AccessRule>(r => r.AllowsExtensions && r.MaxExtensionDurationSeconds == 3600));
    }

    private static SutProvider<CreateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<CreateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
