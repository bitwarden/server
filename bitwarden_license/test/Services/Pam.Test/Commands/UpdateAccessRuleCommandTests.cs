using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class UpdateAccessRuleCommandTests
{
    private static readonly DateTime _now = new(2026, 5, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task UpdateAsync_HappyPath_UpdatesFieldsAndBumpsRevision(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        existing.CollectionIds = [];
        update.Name = "renamed";
        update.Description = "new description";
        update.Conditions = """{"kind":"human_approval"}""";
        update.SingleActiveLease = true;
        update.DefaultLeaseDurationSeconds = 3600;
        update.MaxLeaseDurationSeconds = 28800;
        update.AllowsExtensions = true;
        update.MaxExtensionDurationSeconds = 7200;
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<AccessRule> { existing });

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, []);

        Assert.Equal("renamed", result.Name);
        Assert.Equal("new description", result.Description);
        Assert.Equal(update.Conditions, result.Conditions);
        Assert.True(result.SingleActiveLease);
        Assert.Equal(3600, result.DefaultLeaseDurationSeconds);
        Assert.Equal(28800, result.MaxLeaseDurationSeconds);
        Assert.True(result.AllowsExtensions);
        Assert.Equal(7200, result.MaxExtensionDurationSeconds);
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .ReplaceAsync(Arg.Is<AccessRule>(r =>
                r.Id == existing.Id && r.Name == "renamed" && r.Description == "new description"
                && r.SingleActiveLease
                && r.DefaultLeaseDurationSeconds == 3600 && r.MaxLeaseDurationSeconds == 28800
                && r.AllowsExtensions && r.MaxExtensionDurationSeconds == 7200));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_AllowsExtensionsWithoutMax_ThrowsBadRequest(AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        update.Name = "renamed";
        update.AllowsExtensions = true;
        update.MaxExtensionDurationSeconds = null;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(update.OrganizationId, update.Id, update, []));
        Assert.Contains("maximum extension length", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-1)]
    public async Task UpdateAsync_AllowsExtensionsWithNonPositiveMax_ThrowsBadRequest(int maxExtensionDurationSeconds, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        update.Name = "renamed";
        update.AllowsExtensions = true;
        update.MaxExtensionDurationSeconds = maxExtensionDurationSeconds;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(update.OrganizationId, update.Id, update, []));
        Assert.Contains("maximum extension length", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ReplacesCollections_AssignsNewAndClearsRemoved(AccessRuleDetails existing,
        AccessRule update, Collection keep, Collection add)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Conditions = """{"kind":"human_approval"}""";
        keep.OrganizationId = orgId;
        keep.AccessRuleId = existing.Id;   // already governed by this rule
        add.OrganizationId = orgId;
        add.AccessRuleId = null;
        var desired = new[] { keep.Id, add.Id };
        var removedId = Guid.NewGuid();
        existing.CollectionIds = [keep.Id, removedId];
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<AccessRule> { existing });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { keep, add });

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, desired);

        Assert.Equal(desired, result.CollectionIds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .SetCollectionAssociationsAsync(orgId, existing.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(desired.OrderBy(x => x))),
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { removedId })));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_EmptyCollections_ClearsAll(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Conditions = """{"kind":"human_approval"}""";
        var currentId = Guid.NewGuid();
        existing.CollectionIds = [currentId];
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<AccessRule> { existing });

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, []);

        Assert.Empty(result.CollectionIds);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .SetCollectionAssociationsAsync(orgId, existing.Id,
                Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()),
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { currentId })));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_CollectionGovernedByAnotherRule_ThrowsBadRequest(AccessRuleDetails existing,
        AccessRule update, AccessRule otherRule, Collection collection)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Conditions = """{"kind":"human_approval"}""";
        otherRule.OrganizationId = orgId;
        otherRule.Name = "other";
        collection.OrganizationId = orgId;
        collection.AccessRuleId = otherRule.Id;   // a different rule
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Conditions)
            .Returns(AccessRuleValidationResult.Valid);
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetManyByOrganizationIdAsync(orgId)
            .Returns(new List<AccessRule> { existing, otherRule });
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection> { collection });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, new[] { collection.Id }));
        Assert.Contains("already governed by another access rule", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_MissingExisting_ThrowsNotFound(AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(Arg.Any<Guid>())
            .Returns((AccessRuleDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), update, []));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_WrongOrg_ThrowsNotFound(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var differentOrg = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(differentOrg, existing.Id, update, []));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_InvalidRule_ThrowsBadRequest(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "ok";
        update.Conditions = """{"kind":"bogus"}""";
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleValidator>()
            .Validate(update.Conditions)
            .Returns(AccessRuleValidationResult.Invalid("nope"));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, []));
        Assert.Equal("nope", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
    }

    private static SutProvider<UpdateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<UpdateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
