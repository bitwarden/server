using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

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
        Assert.Equal(_now, result.RevisionDate);
        await sutProvider.GetDependency<IAccessRuleRepository>().Received(1)
            .ReplaceAsync(Arg.Is<AccessRule>(r =>
                r.Id == existing.Id && r.Name == "renamed" && r.Description == "new description"
                && r.SingleActiveLease));
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
        AccessRule update, Collection collection)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        update.Conditions = """{"kind":"human_approval"}""";
        collection.OrganizationId = orgId;
        collection.AccessRuleId = Guid.NewGuid();   // a different rule
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
