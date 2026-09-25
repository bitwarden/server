using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
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
        update.Conditions = """[{"kind":"human_approval"}]""";
        update.SingleActiveLease = true;
        update.DefaultLeaseDurationSeconds = 3600;
        update.MaxLeaseDurationSeconds = 28800;
        update.AllowsExtensions = true;
        update.MaxExtensionDurationSeconds = 7200;
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        SetupValidator(sutProvider, orgId, existing.Id, []);

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
        // The rule under update is excluded from the validator's uniqueness and conflict checks by its own id.
        await sutProvider.GetDependency<IAccessRuleWriteValidator>().Received(1)
            .ValidateAsync(orgId, update, Arg.Any<IEnumerable<Guid>>(), existing.Id);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ReplacesCollections_AssignsNewAndClearsRemoved(AccessRuleDetails existing,
        AccessRule update, Guid keptId, Guid addedId)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        var desired = new[] { keptId, addedId };
        var removedId = Guid.NewGuid();
        existing.CollectionIds = [keptId, removedId];
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        SetupValidator(sutProvider, orgId, existing.Id, [.. desired]);

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, desired);

        Assert.Equal(desired, result.CollectionIds);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .SetAccessRuleAssociationsAsync(orgId, existing.Id,
                Arg.Is<IEnumerable<Guid>>(ids => ids.OrderBy(x => x).SequenceEqual(desired.OrderBy(x => x))),
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { removedId })));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_EmptyCollections_ClearsAll(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        var orgId = existing.OrganizationId;
        update.Name = "renamed";
        var currentId = Guid.NewGuid();
        existing.CollectionIds = [currentId];
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        SetupValidator(sutProvider, orgId, existing.Id, []);

        var result = await sutProvider.Sut.UpdateAsync(orgId, existing.Id, update, []);

        Assert.Empty(result.CollectionIds);
        await sutProvider.GetDependency<ICollectionRepository>().Received(1)
            .SetAccessRuleAssociationsAsync(orgId, existing.Id,
                Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()),
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(new[] { currentId })));
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_MissingExisting_ThrowsNotFoundWithoutValidating(AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(Arg.Any<Guid>())
            .Returns((AccessRuleDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), update, []));
        // A rule the caller cannot see is a 404 before anything about the payload is judged.
        await sutProvider.GetDependency<IAccessRuleWriteValidator>().DidNotReceiveWithAnyArgs()
            .ValidateAsync(default, default!, default!, default);
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
        await sutProvider.GetDependency<IAccessRuleWriteValidator>().DidNotReceiveWithAnyArgs()
            .ValidateAsync(default, default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_ValidationFails_DoesNotPersist(AccessRuleDetails existing, AccessRule update)
    {
        var sutProvider = SetupSutProvider();
        sutProvider.GetDependency<IAccessRuleRepository>()
            .GetDetailsByIdAsync(existing.Id)
            .Returns(existing);
        sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(Arg.Any<Guid>(), Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), Arg.Any<Guid?>())
            .ThrowsAsync(new BadRequestException("A rule with that name already exists."));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateAsync(existing.OrganizationId, existing.Id, update, []));
        Assert.Equal("A rule with that name already exists.", ex.Message);
        await sutProvider.GetDependency<IAccessRuleRepository>().DidNotReceiveWithAnyArgs().ReplaceAsync(default!);
        await sutProvider.GetDependency<ICollectionRepository>().DidNotReceiveWithAnyArgs()
            .SetAccessRuleAssociationsAsync(default, default, default!, default!);
    }

    private static void SetupValidator(SutProvider<UpdateAccessRuleCommand> sutProvider, Guid organizationId,
        Guid existingRuleId, List<Guid> validatedCollectionIds)
        => sutProvider.GetDependency<IAccessRuleWriteValidator>()
            .ValidateAsync(organizationId, Arg.Any<AccessRule>(), Arg.Any<IEnumerable<Guid>>(), existingRuleId)
            .Returns(validatedCollectionIds);

    private static SutProvider<UpdateAccessRuleCommand> SetupSutProvider()
    {
        var sutProvider = new SutProvider<UpdateAccessRuleCommand>()
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
