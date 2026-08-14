using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

[SutProviderCustomize]
public class ModifyCollectionGroupAccessValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_AllEmpty_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid existingManagerId)
    {
        // The command short-circuits on empty deltas, but the validator must still behave correctly if called directly.
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = existingManagerId, Manage = true }]
            });
        var request = new ModifyCollectionGroupAccessRequest([target], [], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateIdWithinAdd_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var add = new[]
        {
            new CollectionAccessSelection { Id = newGroupId },
            new CollectionAccessSelection { Id = newGroupId, Manage = true }
        };
        var request = new ModifyCollectionGroupAccessRequest([target], add, [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateGroupId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateIdWithinUpdate_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid existingGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = existingGroupId }]
            });
        var update = new[]
        {
            new CollectionAccessSelection { Id = existingGroupId },
            new CollectionAccessSelection { Id = existingGroupId, Manage = true }
        };
        var request = new ModifyCollectionGroupAccessRequest([target], [], update, [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateGroupId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_IdInBothRemoveAndUpdate_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid conflictingGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = conflictingGroupId }]
            });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = conflictingGroupId }], [conflictingGroupId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OverlappingGroupId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddManageWithReadOnlyOrHidePasswords_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid readOnlyGroupId, Guid hidePasswordsGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });

        var readOnlyRequest = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = readOnlyGroupId, Manage = true, ReadOnly = true }], [], [], null, false);
        var readOnlyResult = await sutProvider.Sut.ValidateAsync(readOnlyRequest);
        Assert.True(readOnlyResult.IsError);
        Assert.IsType<InvalidManageAssociation>(readOnlyResult.AsError);

        var hidePasswordsRequest = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = hidePasswordsGroupId, Manage = true, HidePasswords = true }], [], [], null, false);
        var hidePasswordsResult = await sutProvider.Sut.ValidateAsync(hidePasswordsRequest);
        Assert.True(hidePasswordsResult.IsError);
        Assert.IsType<InvalidManageAssociation>(hidePasswordsResult.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateManageWithReadOnlyOrHidePasswords_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid existingGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = existingGroupId }]
            });

        var readOnlyRequest = new ModifyCollectionGroupAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = existingGroupId, Manage = true, ReadOnly = true }], [], null, false);
        var readOnlyResult = await sutProvider.Sut.ValidateAsync(readOnlyRequest);
        Assert.True(readOnlyResult.IsError);
        Assert.IsType<InvalidManageAssociation>(readOnlyResult.AsError);

        var hidePasswordsRequest = new ModifyCollectionGroupAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = existingGroupId, Manage = true, HidePasswords = true }], [], null, false);
        var hidePasswordsResult = await sutProvider.Sut.ValidateAsync(hidePasswordsRequest);
        Assert.True(hidePasswordsResult.IsError);
        Assert.IsType<InvalidManageAssociation>(hidePasswordsResult.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DefaultUserCollection_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Type = CollectionType.DefaultUserCollection },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = newGroupId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotModifyDefaultUserCollectionAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddIdAlreadyExistingMember_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid existingGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = existingGroupId }]
            });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = existingGroupId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupAlreadyHasAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateIdNotExistingMember_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid nonMemberGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = nonMemberGroupId }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupDoesNotHaveAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddTargetDoesNotExist_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = newGroupId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Group>());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupsNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddTargetBelongsToDifferentOrganization_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId, Guid otherOrganizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = newGroupId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Group> { new() { Id = newGroupId, OrganizationId = otherOrganizationId } });

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<GroupsNotInOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingLastManager_WithoutAllowAdminAccess_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid managerGroupId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
            });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest([target], [], [], [managerGroupId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingLastManager_WithAllowAdminAccess_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid managerGroupId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
            });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest([target], [], [], [managerGroupId], null, true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingOnlyManagingGroup_ButUserStillManages_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid managerGroupId, Guid userId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = userId, Manage = true }],
                Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
            });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest([target], [], [], [managerGroupId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdatingOnlyManagerToNonManage_NoOtherManager_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid managerGroupId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [],
                Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
            });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = managerGroupId, Manage = false }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddingNewManagerToOrphanedCollection_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails { Users = [], Groups = [] });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            [target], [new CollectionAccessSelection { Id = newGroupId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidDelta_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider,
        Guid existingGroupId, Guid newGroupId, Guid removedGroupId, Guid organizationId)
    {
        var target = new CollectionGroupAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [],
                Groups =
                [
                    new CollectionAccessSelection { Id = existingGroupId },
                    new CollectionAccessSelection { Id = removedGroupId }
                ]
            });
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            [target],
            [new CollectionAccessSelection { Id = newGroupId }],
            [new CollectionAccessSelection { Id = existingGroupId, Manage = true }],
            [removedGroupId],
            null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_AnyDefaultUserCollection_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId)
    {
        var targets = new[]
        {
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
                new CollectionAccessDetails { Users = [], Groups = [] }),
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Type = CollectionType.DefaultUserCollection },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        var request = new ModifyCollectionGroupAccessRequest(
            targets, [new CollectionAccessSelection { Id = newGroupId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotModifyDefaultUserCollectionAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_AlreadyMemberOfOneNotTheOther_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid existingGroupId, Guid organizationId)
    {
        // The Add-must-be-new check only applies to a single collection. Across multiple targets the same
        // group can already have access to one and not another, so we don't check it here.
        var targets = new[]
        {
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [],
                    Groups = [new CollectionAccessSelection { Id = existingGroupId }]
                }),
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            targets, [new CollectionAccessSelection { Id = existingGroupId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_RemovingOnlyManagerOfOneTarget_ReturnsError(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider,
        Guid managerGroupId, Guid otherManagerGroupId, Guid organizationId)
    {
        var targets = new[]
        {
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [],
                    Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
                }),
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [],
                    Groups = [new CollectionAccessSelection { Id = otherManagerGroupId, Manage = true }]
                })
        };
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(targets, [], [], [managerGroupId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_ValidDelta_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid newGroupId, Guid organizationId)
    {
        var targets = new[]
        {
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] }),
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            targets, [new CollectionAccessSelection { Id = newGroupId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_UpdateGrantsNewManagerOnOtherTarget_Succeeds(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid managerGroupId, Guid organizationId)
    {
        // Regression test. An Update entry for a group that isn't yet a member of a target still upserts onto
        // that target, since the same delta applies to every collection. It has to count toward that target's
        // remaining manage access, or a valid request gets rejected as leaving the second target unmanaged.
        var targets = new[]
        {
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [],
                    Groups = [new CollectionAccessSelection { Id = managerGroupId, Manage = true }]
                }),
            new CollectionGroupAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidGroups(sutProvider, organizationId);
        var request = new ModifyCollectionGroupAccessRequest(
            targets, [], [new CollectionAccessSelection { Id = managerGroupId, Manage = true }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    // Any id passed to GetManyByManyIds resolves as a valid group in the given org, unless overridden.
    private static void ArrangeValidGroups(
        SutProvider<ModifyCollectionGroupAccessValidator> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(id => new Group { Id = id, OrganizationId = organizationId })
                .ToList());
    }
}
