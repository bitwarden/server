using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class UpdateOrganizationUserCommandTests
{
    [Theory, BitAutoData]
    public async Task UpdateUserAsync_NoUserId_Throws(OrganizationUser user, Guid? savingUserId,
        List<CollectionAccessSelection> collections, List<Guid> groups, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        user.Id = default(Guid);
        var existingUserType = OrganizationUserType.User;

        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_DifferentOrganizationId_Throws(OrganizationUser user, OrganizationUser originalUser,
        Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(user.Id).Returns(originalUser);
        var existingUserType = OrganizationUserType.User;

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, null, null));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_CollectionsBelongToDifferentOrganization_Throws(OrganizationUser user, OrganizationUser originalUser,
        List<CollectionAccessSelection> collectionAccess, Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization)
    {
        Setup(sutProvider, organization, user, originalUser);

        // Return collections with different organizationIds from the repository
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        var existingUserType = OrganizationUserType.User;

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, collectionAccess, null));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_CollectionsDoNotExist_Throws(OrganizationUser user, OrganizationUser originalUser,
        List<CollectionAccessSelection> collectionAccess, Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization)
    {
        Setup(sutProvider, organization, user, originalUser);

        // Return matching collections, except that 1 is missing
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Collection { Id = guid, OrganizationId = user.OrganizationId }).ToList();
                result.RemoveAt(0);
                return result;
            });
        var existingUserType = OrganizationUserType.User;
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, collectionAccess, null));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_GroupsBelongToDifferentOrganization_Throws(OrganizationUser user, OrganizationUser originalUser,
        ICollection<Guid> groupAccess, Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization)
    {
        Setup(sutProvider, organization, user, originalUser);

        // Return collections with different organizationIds from the repository
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Group { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList());

        var existingUserType = OrganizationUserType.User;

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, null, groupAccess));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_GroupsDoNotExist_Throws(OrganizationUser user, OrganizationUser originalUser,
        ICollection<Guid> groupAccess, Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider,
        Organization organization)
    {
        Setup(sutProvider, organization, user, originalUser);

        // Return matching collections, except that 1 is missing
        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var result = callInfo.Arg<IEnumerable<Guid>>()
                    .Select(guid => new Group { Id = guid, OrganizationId = CoreHelpers.GenerateComb() }).ToList();
                result.RemoveAt(0);
                return result;
            });
        var existingUserType = OrganizationUserType.User;
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, existingUserType, savingUserId, null, groupAccess));
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_Passes(
        Organization organization,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        List<CollectionAccessSelection> collections,
        List<Guid> groups,
        Permissions permissions,
        [OrganizationUser(type: OrganizationUserType.Owner)] OrganizationUser savingUser,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        Setup(sutProvider, organization, newUserData, oldUserData);

        // Arrange list of collections to make sure Manage is mutually exclusive
        for (var i = 0; i < collections.Count; i++)
        {
            var cas = collections[i];
            cas.Manage = i != collections.Count - 1;
            cas.HidePasswords = i == collections.Count - 1;
            cas.ReadOnly = i == collections.Count - 1;
        }

        newUserData.Id = oldUserData.Id;
        newUserData.UserId = oldUserData.UserId;
        newUserData.OrganizationId = savingUser.OrganizationId = oldUserData.OrganizationId = organization.Id;
        newUserData.Type = OrganizationUserType.Admin;
        newUserData.Permissions = JsonSerializer.Serialize(permissions, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Collection { Id = guid, OrganizationId = oldUserData.OrganizationId }).ToList());

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByManyIds(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>()
                .Select(guid => new Group { Id = guid, OrganizationId = oldUserData.OrganizationId }).ToList());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(newUserData.Id)
            .Returns(0);

        var existingUserType = OrganizationUserType.User;

        await sutProvider.Sut.UpdateUserAsync(newUserData, existingUserType, savingUser.UserId, collections, groups);

        var organizationService = sutProvider.GetDependency<IOrganizationService>();
        await organizationService.Received(1).ValidateOrganizationUserUpdatePermissions(
            newUserData.OrganizationId,
            newUserData.Type,
            oldUserData.Type,
            Arg.Any<Permissions>());
        await organizationService.Received(1).ValidateOrganizationCustomPermissionsEnabledAsync(
            newUserData.OrganizationId,
            newUserData.Type);
        await sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>().Received(1).HasConfirmedOwnersExceptAsync(
            newUserData.OrganizationId,
            Arg.Is<IEnumerable<Guid>>(i => i.Contains(newUserData.Id)));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenUpdatingUserToAdminOrOwner_AndExistingUserTypeIsNotAdminOrOwner_WithUserAlreadyAdminOfAnotherFreeOrganization_Throws(
        OrganizationUserType userType,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        Organization organization,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.Free;
        newUserData.Type = userType;

        Setup(sutProvider, organization, newUserData, oldUserData);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(newUserData.UserId!.Value)
            .Returns(1);
        var existingUserType = OrganizationUserType.User;

        // Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(newUserData, existingUserType, null, null, null));
        Assert.Contains("User can only be an admin of one free organization.", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Admin, OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner, OrganizationUserType.Owner)]
    public async Task UpdateUserAsync_WhenUpdatingUserToAdminOrOwner_AndExistingUserTypeIsAdminOrOwner_WithUserAlreadyAdminOfAnotherFreeOrganization_Throws(
        OrganizationUserType newUserType,
        OrganizationUserType existingUserType,
        OrganizationUser oldUserData,
        OrganizationUser newUserData,
        Organization organization,
        SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        organization.PlanType = PlanType.Free;
        newUserData.Type = newUserType;

        Setup(sutProvider, organization, newUserData, oldUserData);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetCountByFreeOrganizationAdminUserAsync(newUserData.UserId!.Value)
            .Returns(2);

        // Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(newUserData, existingUserType, null, null, null));
        Assert.Contains("User can only be an admin of one free organization.", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_WithMixedCollectionTypes_FiltersOutDefaultUserCollections(
        OrganizationUser user, OrganizationUser originalUser, Collection sharedCollection, Collection defaultUserCollection,
        Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider, Organization organization)
    {
        user.Permissions = null;
        sharedCollection.Type = CollectionType.SharedCollection;
        defaultUserCollection.Type = CollectionType.DefaultUserCollection;
        sharedCollection.OrganizationId = defaultUserCollection.OrganizationId = organization.Id;

        Setup(sutProvider, organization, user, originalUser);

        var collectionAccess = new List<CollectionAccessSelection>
        {
            new() { Id = sharedCollection.Id, ReadOnly = true, HidePasswords = false, Manage = false },
            new() { Id = defaultUserCollection.Id, ReadOnly = false, HidePasswords = true, Manage = false }
        };

        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection>
            {
                new() { Id = sharedCollection.Id, OrganizationId = user.OrganizationId, Type = CollectionType.SharedCollection },
                new() { Id = defaultUserCollection.Id, OrganizationId = user.OrganizationId, Type = CollectionType.DefaultUserCollection }
            });

        await sutProvider.Sut.UpdateUserAsync(user, OrganizationUserType.User, savingUserId, collectionAccess, null);

        // Verify that ReplaceAsync was called with only the shared collection (default user collection filtered out)
        await sutProvider.GetDependency<IOrganizationUserRepository>().Received(1).ReplaceAsync(
            user,
            Arg.Is<IEnumerable<CollectionAccessSelection>>(collections =>
                collections.Count() == 1 &&
                collections.First().Id == sharedCollection.Id
            )
        );
    }

    private void Setup(SutProvider<UpdateOrganizationUserCommand> sutProvider, Organization organization,
        OrganizationUser newUser, OrganizationUser oldUser)
    {
        var organizationRepository = sutProvider.GetDependency<IOrganizationRepository>();
        var organizationUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var organizationService = sutProvider.GetDependency<IOrganizationService>();

        organizationRepository.GetByIdAsync(organization.Id).Returns(organization);

        newUser.Id = oldUser.Id;
        newUser.UserId = oldUser.UserId;
        newUser.OrganizationId = oldUser.OrganizationId = organization.Id;
        organizationUserRepository.GetByIdAsync(oldUser.Id).Returns(oldUser);
        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(
                oldUser.OrganizationId,
                Arg.Is<IEnumerable<Guid>>(i => i.Contains(oldUser.Id)))
            .Returns(true);
    }
}
