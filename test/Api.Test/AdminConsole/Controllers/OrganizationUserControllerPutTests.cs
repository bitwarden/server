using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
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
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(false);

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, true);

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
    public async Task Put_UpdateSelf_WithoutAllowAdminAccessToAllCollectionItems_CannotAddSelfToCollections(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, false);

        // User is not currently assigned to any collections, which means they're adding themselves
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUser.Id)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser,
                new List<CollectionAccessSelection>()));
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<Collection>());

        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        var exception = await Assert.ThrowsAsync<BadRequestException>(async () => await sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model));
        Assert.Contains("You cannot add yourself to a collection.", exception.Message);
    }
    [Theory]
    [BitAutoData]
    public async Task Put_UpdateSelf_WithoutAllowAdminAccessToAllCollectionItems_DoesNotUpdateGroups(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, true);

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
            null);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_UpdateSelf_WithAllowAdminAccessToAllCollectionItems_DoesUpdateGroups(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        // Updating self
        organizationUser.UserId = savingUserId;
        organizationAbility.AllowAdminAccessToAllCollectionItems = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, true);

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
    public async Task Put_UpdateCollections_OnlyUpdatesCollectionsTheSavingUserCanUpdate(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);
        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, false);

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

        // User is upgrading editedCollectionId to manage
        model.Collections = new List<SelectionReadOnlyRequestModel>
        {
            new() { Id = editedCollectionId, HidePasswords = false, Manage = true, ReadOnly = false }
        };

        // Save these for later - organizationUser object will be mutated
        var orgUserId = organizationUser.Id;
        var orgUserEmail = organizationUser.Email;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUser.Id)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser,
                currentCollectionAccess));

        var currentCollections = currentCollectionAccess
            .Select(cas => new Collection { Id = cas.Id }).ToList();
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(currentCollections);

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
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);
        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, false);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByIdWithCollectionsAsync(organizationUser.Id)
            .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser,
                model.Collections.Select(cas => cas.ToSelectionReadOnly()).ToList()));
        var collections = model.Collections.Select(cas => new Collection { Id = cas.Id }).ToList();
        sutProvider.GetDependency<ICollectionRepository>()
            .GetManyByManyIdsAsync(Arg.Is<IEnumerable<Guid>>(guids => guids.SequenceEqual(collections.Select(c => c.Id))))
            .Returns(collections);

        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => collections.Contains(c)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.Put(organizationAbility.Id, organizationUser.Id, model));
        Assert.Contains("You must have Can Manage permission", exception.Message);
    }

    private void Put_Setup(SutProvider<OrganizationUsersController> sutProvider, OrganizationAbility organizationAbility,
        OrganizationUser organizationUser, Guid savingUserId, OrganizationUserUpdateRequestModel model, bool authorizeAll)
    {
        var orgId = organizationAbility.Id = organizationUser.OrganizationId;

        sutProvider.GetDependency<ICurrentContext>().ManageUsers(orgId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByIdAsync(organizationUser.Id).Returns(organizationUser);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(orgId)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(savingUserId);

        if (authorizeAll)
        {
            // Simple case: saving user can edit all collections, all collection access is replaced
            sutProvider.GetDependency<IOrganizationUserRepository>()
                .GetByIdWithCollectionsAsync(organizationUser.Id)
                .Returns(new Tuple<OrganizationUser, ICollection<CollectionAccessSelection>>(organizationUser,
                    model.Collections.Select(cas => cas.ToSelectionReadOnly()).ToList()));
            var collections = model.Collections.Select(cas => new Collection { Id = cas.Id }).ToList();
            sutProvider.GetDependency<ICollectionRepository>()
                .GetManyByManyIdsAsync(Arg.Is<IEnumerable<Guid>>(guids => guids.SequenceEqual(collections.Select(c => c.Id))))
                .Returns(collections);

            sutProvider.GetDependency<IAuthorizationService>()
                .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Is<Collection>(c => collections.Contains(c)),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(r => r.Contains(BulkCollectionOperations.ModifyUserAccess)))
                .Returns(AuthorizationResult.Success());
        }
    }
}
