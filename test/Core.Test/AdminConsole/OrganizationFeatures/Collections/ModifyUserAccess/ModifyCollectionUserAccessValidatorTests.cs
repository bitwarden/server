using Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

[SutProviderCustomize]
public class ModifyCollectionUserAccessValidatorTests
{
    [Theory, BitAutoData]
    public async Task ValidateAsync_AllEmpty_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid existingManagerId)
    {
        // The command short-circuits on empty deltas, but the validator must still behave correctly if called directly.
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = existingManagerId, Manage = true }],
                Groups = []
            });
        var request = new ModifyCollectionUserAccessRequest([target], [], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateIdWithinAdd_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var add = new[]
        {
            new CollectionAccessSelection { Id = newUserId },
            new CollectionAccessSelection { Id = newUserId, Manage = true }
        };
        var request = new ModifyCollectionUserAccessRequest([target], add, [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateOrganizationUserId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DuplicateIdWithinUpdate_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid existingUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = existingUserId }],
                Groups = []
            });
        var update = new[]
        {
            new CollectionAccessSelection { Id = existingUserId },
            new CollectionAccessSelection { Id = existingUserId, Manage = true }
        };
        var request = new ModifyCollectionUserAccessRequest([target], [], update, [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<DuplicateOrganizationUserId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_IdInBothRemoveAndUpdate_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid conflictingUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = conflictingUserId }],
                Groups = []
            });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = conflictingUserId }], [conflictingUserId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OverlappingOrganizationUserId>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddManageWithReadOnlyOrHidePasswords_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid readOnlyUserId, Guid hidePasswordsUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });

        var readOnlyRequest = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = readOnlyUserId, Manage = true, ReadOnly = true }], [], [], null, false);
        var readOnlyResult = await sutProvider.Sut.ValidateAsync(readOnlyRequest);
        Assert.True(readOnlyResult.IsError);
        Assert.IsType<InvalidManageAssociation>(readOnlyResult.AsError);

        var hidePasswordsRequest = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = hidePasswordsUserId, Manage = true, HidePasswords = true }], [], [], null, false);
        var hidePasswordsResult = await sutProvider.Sut.ValidateAsync(hidePasswordsRequest);
        Assert.True(hidePasswordsResult.IsError);
        Assert.IsType<InvalidManageAssociation>(hidePasswordsResult.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateManageWithReadOnlyOrHidePasswords_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid existingUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = existingUserId }],
                Groups = []
            });

        var readOnlyRequest = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = existingUserId, Manage = true, ReadOnly = true }], [], null, false);
        var readOnlyResult = await sutProvider.Sut.ValidateAsync(readOnlyRequest);
        Assert.True(readOnlyResult.IsError);
        Assert.IsType<InvalidManageAssociation>(readOnlyResult.AsError);

        var hidePasswordsRequest = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = existingUserId, Manage = true, HidePasswords = true }], [], null, false);
        var hidePasswordsResult = await sutProvider.Sut.ValidateAsync(hidePasswordsRequest);
        Assert.True(hidePasswordsResult.IsError);
        Assert.IsType<InvalidManageAssociation>(hidePasswordsResult.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_DefaultUserCollection_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Type = CollectionType.DefaultUserCollection },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = newUserId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotModifyDefaultUserCollectionAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddIdAlreadyExistingMember_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid existingUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = existingUserId }],
                Groups = []
            });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = existingUserId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserAlreadyHasAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdateIdNotExistingMember_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid nonMemberUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = nonMemberUserId }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUserDoesNotHaveAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddTargetDoesNotExist_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = newUserId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser>());

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUsersNotFound>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddTargetBelongsToDifferentOrganization_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId, Guid otherOrganizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
            new CollectionAccessDetails { Users = [], Groups = [] });
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = newUserId, Manage = true }], [], [], null, false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns([new OrganizationUser { Id = newUserId, OrganizationId = otherOrganizationId }]);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<OrganizationUsersNotInOrganization>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddingSelfAsNewMember_WithoutAllowAdminAccess_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid performingId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails { Users = [], Groups = [] });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = performingId, Manage = true }], [], [], performingId, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAddSelfToCollection>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddingSelfAsNewMember_WithAllowAdminAccess_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid performingId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails { Users = [], Groups = [] });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = performingId, Manage = true }], [], [], performingId, true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ChangingOwnExistingAccess_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid performingId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = performingId }],
                Groups = []
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = performingId, Manage = true }], [], performingId, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingLastManager_WithoutAllowAdminAccess_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid managerId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                Groups = []
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest([target], [], [], [managerId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingLastManager_WithAllowAdminAccess_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid managerId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                Groups = []
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest([target], [], [], [managerId], null, true);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_RemovingOnlyManagingUser_ButGroupStillManages_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid managerId, Guid groupId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                Groups = [new CollectionAccessSelection { Id = groupId, Manage = true }]
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest([target], [], [], [managerId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_UpdatingOnlyManagerToNonManage_NoOtherManager_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid managerId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                Groups = []
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target], [], [new CollectionAccessSelection { Id = managerId, Manage = false }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_AddingNewManagerToOrphanedCollection_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails { Users = [], Groups = [] });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target], [new CollectionAccessSelection { Id = newUserId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_ValidDelta_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider,
        Guid existingUserId, Guid newUserId, Guid removedUserId, Guid organizationId)
    {
        var target = new CollectionUserAccessTarget(
            new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
            new CollectionAccessDetails
            {
                Users =
                [
                    new CollectionAccessSelection { Id = existingUserId },
                    new CollectionAccessSelection { Id = removedUserId }
                ],
                Groups = []
            });
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            [target],
            [new CollectionAccessSelection { Id = newUserId }],
            [new CollectionAccessSelection { Id = existingUserId, Manage = true }],
            [removedUserId],
            null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_AnyDefaultUserCollection_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId)
    {
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid() },
                new CollectionAccessDetails { Users = [], Groups = [] }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Type = CollectionType.DefaultUserCollection },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        var request = new ModifyCollectionUserAccessRequest(
            targets, [new CollectionAccessSelection { Id = newUserId }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotModifyDefaultUserCollectionAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_AlreadyMemberOfOneNotTheOther_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid existingUserId, Guid organizationId)
    {
        // The Add-must-be-new check only applies to a single collection. Across multiple targets the same
        // user can already have access to one and not another, so we don't check it here.
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = existingUserId }],
                    Groups = []
                }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            targets, [new CollectionAccessSelection { Id = existingUserId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_RemovingOnlyManagerOfOneTarget_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider,
        Guid managerId, Guid otherManagerId, Guid organizationId)
    {
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                    Groups = []
                }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = otherManagerId, Manage = true }],
                    Groups = []
                })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(targets, [], [], [managerId], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<NoRemainingManageAccess>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_AddingSelfAsNewMemberOnOneTarget_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid performingId, Guid organizationId)
    {
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = performingId }],
                    Groups = []
                }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            targets, [new CollectionAccessSelection { Id = performingId }], [], [], performingId, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAddSelfToCollection>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_ValidDelta_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid newUserId, Guid organizationId)
    {
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            targets, [new CollectionAccessSelection { Id = newUserId, Manage = true }], [], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_UpdatingSelfAsNewMemberOnOneTarget_ReturnsError(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid performingId, Guid organizationId)
    {
        // Regression test. Putting the performing user's own id in Update instead of Add must not bypass the
        // self-add guard. The same delta hits every target, so this would otherwise give the performing user
        // brand-new access on the second target.
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = performingId }],
                    Groups = []
                }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            targets, [], [new CollectionAccessSelection { Id = performingId, Manage = true }], [], performingId, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsError);
        Assert.IsType<CannotAddSelfToCollection>(result.AsError);
    }

    [Theory, BitAutoData]
    public async Task ValidateAsync_MultipleTargets_UpdateGrantsNewManagerOnOtherTarget_Succeeds(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid managerId, Guid organizationId)
    {
        // Regression test. An Update entry for a user who isn't yet a member of a target still upserts onto
        // that target, since the same delta applies to every collection. It has to count toward that target's
        // remaining manage access, or a valid request gets rejected as leaving the second target unmanaged.
        var targets = new[]
        {
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails
                {
                    Users = [new CollectionAccessSelection { Id = managerId, Manage = true }],
                    Groups = []
                }),
            new CollectionUserAccessTarget(
                new Collection { Id = Guid.NewGuid(), OrganizationId = organizationId },
                new CollectionAccessDetails { Users = [], Groups = [] })
        };
        ArrangeValidOrganizationUsers(sutProvider, organizationId);
        var request = new ModifyCollectionUserAccessRequest(
            targets, [], [new CollectionAccessSelection { Id = managerId, Manage = true }], [], null, false);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.True(result.IsValid);
    }

    // Any id passed to GetManyAsync resolves as a valid organization user in the given org, unless overridden.
    private static void ArrangeValidOrganizationUsers(
        SutProvider<ModifyCollectionUserAccessValidator> sutProvider, Guid organizationId)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(id => new OrganizationUser { Id = id, OrganizationId = organizationId })
                .ToList());
    }
}
