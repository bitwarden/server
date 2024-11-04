using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
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
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collections, groups));
        Assert.Contains("invite the user first", exception.Message.ToLowerInvariant());
    }

    [Theory, BitAutoData]
    public async Task UpdateUserAsync_DifferentOrganizationId_Throws(OrganizationUser user, OrganizationUser originalUser,
        Guid? savingUserId, SutProvider<UpdateOrganizationUserCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(user.Id).Returns(originalUser);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, null, null));
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collectionAccess, null));
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, collectionAccess, null));
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, null, groupAccess));
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

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateUserAsync(user, savingUserId, null, groupAccess));
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

        await sutProvider.Sut.UpdateUserAsync(newUserData, savingUser.UserId, collections, groups);

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
