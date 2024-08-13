using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Controllers;

[ControllerCustomize(typeof(OrganizationUsersController))]
[SutProviderCustomize]
public class OrganizationUserControllerPutTests
{
    [Theory]
    [BitAutoData]
    public async Task Put_Success(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess: []);

        // Authorize all changes for basic happy path test
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<Collection>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Success());

        // Save these for later - organizationUser object will be mutated
        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model);

        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(Arg.Is<OrganizationUser>(ou =>
                ou.Type == model.Type &&
                ou.Permissions == CoreHelpers.ClassToJsonData(model.Permissions) &&
                ou.AccessSecretsManager == model.AccessSecretsManager &&
                ou.Id == orgUserId &&
                ou.Email == orgUserEmail),
            savingUserId,
            Arg.Is<List<CollectionAccessSelection>>(cas =>
                cas.All(c => model.Collections.Any(m => m.Id == c.Id))),
            model.Groups);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_NoAdminAccess_CannotAddSelfToCollections(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = false;

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess: []);

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model));
        Assert.Contains("You cannot add yourself to a collection.", exception.Message);
    }
    [Theory]
    [BitAutoData]
    public async Task Put_NoAdminAccess_CannotAddSelfToGroups(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = false;

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess: []);

        // Not changing any collection access
        model.Collections = new List<SelectionReadOnlyRequestModel>();

        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model);

        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(Arg.Is<OrganizationUser>(ou =>
            ou.Type == model.Type &&
            ou.Permissions == CoreHelpers.ClassToJsonData(model.Permissions) &&
            ou.AccessSecretsManager == model.AccessSecretsManager &&
            ou.Id == orgUserId &&
            ou.Email == orgUserEmail),
            savingUserId,
            Arg.Is<List<CollectionAccessSelection>>(cas =>
                cas.All(c => model.Collections.Any(m => m.Id == c.Id))),
            // Main assertion: groups are not updated (are null)
            null);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_WithAdminAccess_CanAddSelfToGroups(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = true;

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess: []);

        // Not changing any collection access
        model.Collections = new List<SelectionReadOnlyRequestModel>();

        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model);

        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(Arg.Is<OrganizationUser>(ou =>
            ou.Type == model.Type &&
            ou.Permissions == CoreHelpers.ClassToJsonData(model.Permissions) &&
            ou.AccessSecretsManager == model.AccessSecretsManager &&
            ou.Id == orgUserId &&
            ou.Email == orgUserEmail),
            savingUserId,
            Arg.Is<List<CollectionAccessSelection>>(cas =>
                cas.All(c => model.Collections.Any(m => m.Id == c.Id))),
            model.Groups);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_DoesNotOverwriteUnauthorizedCollections(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        var editedCollectionId = CoreHelpers.GenerateComb();
        var readonlyCollectionId1 = CoreHelpers.GenerateComb();
        var readonlyCollectionId2 = CoreHelpers.GenerateComb();

        var currentCollectionAccess = new List<CollectionAccessSelection>
        {
            new()
            {
                Id = editedCollectionId,
                HidePasswords = true,
                Manage = false,
                ReadOnly = true
            },
            new()
            {
                Id = readonlyCollectionId1,
                HidePasswords = false,
                Manage = true,
                ReadOnly = false
            },
            new()
            {
                Id = readonlyCollectionId2,
                HidePasswords = false,
                Manage = false,
                ReadOnly = false
            },
        };

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess);

        // User is upgrading editedCollectionId to manage
        model.Collections = new List<SelectionReadOnlyRequestModel>
        {
            new() { Id = editedCollectionId, HidePasswords = false, Manage = true, ReadOnly = false }
        };

        // Save these for later - organizationUser object will be mutated
        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        // Authorize the editedCollection
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => c.Id == editedCollectionId),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Success());

        // Do not authorize the readonly collections
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => c.Id == readonlyCollectionId1 || c.Id == readonlyCollectionId2),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());

        await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model);

        // Expect all collection access (modified and unmodified) to be saved
        await sutProvider.GetDependency<IUpdateOrganizationUserCommand>().Received(1).UpdateUserAsync(Arg.Is<OrganizationUser>(ou =>
            ou.Type == model.Type &&
            ou.Permissions == CoreHelpers.ClassToJsonData(model.Permissions) &&
            ou.AccessSecretsManager == model.AccessSecretsManager &&
            ou.Id == orgUserId &&
            ou.Email == orgUserEmail),
            savingUserId,
            Arg.Is<List<CollectionAccessSelection>>(cas =>
                cas.Select(c => c.Id).SequenceEqual(currentCollectionAccess.Select(c => c.Id)) &&
                cas.First(c => c.Id == editedCollectionId).Manage == true &&
                cas.First(c => c.Id == editedCollectionId).ReadOnly == false &&
                cas.First(c => c.Id == editedCollectionId).HidePasswords == false),
            model.Groups);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_ThrowsIfSavingUserCannotUpdateCollections(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Target user is currently assigned to the POSTed collections
        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId,
            currentCollectionAccess: model.Collections.Select(cas => cas.ToSelectionReadOnly()).ToList());

        var postedCollectionIds = model.Collections.Select(c => c.Id).ToHashSet();

        // But the saving user does not have permission to update them
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => postedCollectionIds.Contains(c.Id)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateCollections_ThrowsIfSavingUserCannotAddCollections(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // The target user is not currently assigned to any collections, so we're granting access for the first time
        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, currentCollectionAccess: []);

        var postedCollectionIds = model.Collections.Select(c => c.Id).ToHashSet();
        // But the saving user does not have permission to assign access to the collections
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => postedCollectionIds.Contains(c.Id)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model));
    }

    private void Put_Setup(SutProvider<OrganizationUsersController> sutProvider,
        OrganizationAbility organizationAbility, OrganizationUser organizationUser, Guid savingUserId,
        List<CollectionAccessSelection> currentCollectionAccess)
    {
        var orgId = organizationAbility.Id = organizationUser.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id)
            .Returns(organizationUser);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);

        // OrganizationUserRepository: return the user with current collection access
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUser.Id)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser,
                currentCollectionAccess ?? []));

        // Collection repository: return mock Collection objects for any ids passed in
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo => callInfo.Arg<IEnumerable<Guid>>().Select(guid => new Collection { Id = guid }).ToList());
    }
}
