using System.Security.Claims;
using Bit.Api.AdminConsole.Controllers;
using Bit.Api.AdminConsole.Models.Request.Organizations;
using Bit.Api.Models.Request;
using Bit.Api.Vault.AuthorizationHandlers.Collections;
using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;
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
public class OrganizationUsersControllerTests
{
    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_InivitedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Invited;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1).AcceptOrgUserByOrgIdAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task PutResetPasswordEnrollment_ConfirmedUser_AcceptsInvite(Guid orgId, Guid userId, OrganizationUserResetPasswordEnrollmentRequestModel model,
        User user, OrganizationUser orgUser, SutProvider<OrganizationUsersController> sutProvider)
    {
        orgUser.Status = Core.Enums.OrganizationUserStatusType.Confirmed;
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetByOrganizationAsync(default, default).ReturnsForAnyArgs(orgUser);

        await sutProvider.Sut.PutResetPasswordEnrollment(orgId, userId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(0).AcceptOrgUserByOrgIdAsync(orgId, user, sutProvider.GetDependency<IUserService>());
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_RequiresKnownUser(Guid orgId, Guid orgUserId, OrganizationUserAcceptRequestModel model,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.Accept(orgId, orgUserId, model));
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_NoMasterPasswordReset(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);

        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().DidNotReceiveWithAnyArgs()
            .UpdateUserResetPasswordEnrollmentAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData]
    public async Task Accept_RequireMasterPasswordReset(Guid orgId, Guid orgUserId,
        OrganizationUserAcceptRequestModel model, User user, SutProvider<OrganizationUsersController> sutProvider)
    {
        var policy = new Policy
        {
            Enabled = true,
            Data = CoreHelpers.ClassToJsonData(new ResetPasswordDataModel { AutoEnrollEnabled = true, }),
        };
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<IPolicyRepository>().GetByOrganizationIdTypeAsync(orgId,
            PolicyType.ResetPassword).Returns(policy);

        await sutProvider.Sut.Accept(orgId, orgUserId, model);

        await sutProvider.GetDependency<IAcceptOrgUserCommand>().Received(1)
            .AcceptOrgUserByEmailTokenAsync(orgUserId, user, model.Token, sutProvider.GetDependency<IUserService>());
        await sutProvider.GetDependency<IOrganizationService>().Received(1)
            .UpdateUserResetPasswordEnrollmentAsync(orgId, user.Id, model.ResetPasswordKey, user.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task Invite_Success(OrganizationAbility organizationAbility, OrganizationUserInviteRequestModel model,
        Guid userId, SutProvider<OrganizationUsersController> sutProvider)
    {
        organizationAbility.FlexibleCollections = true;
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationAbility.Id).Returns(true);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Success());
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        await sutProvider.Sut.Invite(organizationAbility.Id, model);

        await sutProvider.GetDependency<IOrganizationService>().Received(1).InviteUsersAsync(organizationAbility.Id,
            userId, Arg.Is<IEnumerable<(OrganizationUserInvite, string)>>(invites =>
                invites.Count() == 1 &&
                invites.First().Item1.Emails.SequenceEqual(model.Emails) &&
                invites.First().Item1.Type == model.Type &&
                invites.First().Item1.AccessSecretsManager == model.AccessSecretsManager));
    }

    [Theory]
    [BitAutoData]
    public async Task Invite_NotAuthorizedToGiveAccessToCollections_Throws(OrganizationAbility organizationAbility, OrganizationUserInviteRequestModel model,
        Guid userId, SutProvider<OrganizationUsersController> sutProvider)
    {
        organizationAbility.FlexibleCollections = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ManageUsers(organizationAbility.Id).Returns(true);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);
        sutProvider.GetDependency<IAuthorizationService>()
            .AuthorizeAsync(Arg.Any<ClaimsPrincipal>(),
                Arg.Any<IEnumerable<Collection>>(),
                Arg.Is<IEnumerable<IAuthorizationRequirement>>(reqs => reqs.Contains(BulkCollectionOperations.ModifyUserAccess)))
            .Returns(AuthorizationResult.Failed());
        sutProvider.GetDependency<IUserService>().GetProperUserId(Arg.Any<ClaimsPrincipal>()).Returns(userId);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.Invite(organizationAbility.Id, model));
        Assert.Contains("You are not authorized", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task Put_Success(OrganizationUserUpdateRequestModel model,
        OrganizationUser organizationUser, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider, Guid savingUserId)
    {
        organizationAbility.FlexibleCollections = false;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(false);

        Put_Setup(sutProvider, organizationAbility, organizationUser, savingUserId, model, false);

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
        organizationAbility.FlexibleCollections = true;
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
        organizationAbility.FlexibleCollections = true;
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
        organizationAbility.FlexibleCollections = true;
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
        organizationAbility.FlexibleCollections = true;
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
        organizationAbility.FlexibleCollections = true;
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

    [Theory]
    [BitAutoData]
    public async Task Get_WithFlexibleCollections_ReturnsUsers(
        ICollection<OrganizationUserUserDetails> organizationUsers, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        Get_Setup(organizationAbility, organizationUsers, sutProvider);
        var response = await sutProvider.Sut.Get(organizationAbility.Id);

        Assert.True(response.Data.All(r => organizationUsers.Any(ou => ou.Id == r.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WithFlexibleCollections_HandlesNullPermissionsObject(
        ICollection<OrganizationUserUserDetails> organizationUsers, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        Get_Setup(organizationAbility, organizationUsers, sutProvider);
        organizationUsers.First().Permissions = "null";
        var response = await sutProvider.Sut.Get(organizationAbility.Id);

        Assert.True(response.Data.All(r => organizationUsers.Any(ou => ou.Id == r.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WithFlexibleCollections_SetsDeprecatedCustomPermissionstoFalse(
        ICollection<OrganizationUserUserDetails> organizationUsers, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        Get_Setup(organizationAbility, organizationUsers, sutProvider);

        var customUser = organizationUsers.First();
        customUser.Type = OrganizationUserType.Custom;
        customUser.Permissions = CoreHelpers.ClassToJsonData(new Permissions
        {
            AccessReports = true,
            EditAssignedCollections = true,
            DeleteAssignedCollections = true,
            AccessEventLogs = true
        });

        var response = await sutProvider.Sut.Get(organizationAbility.Id);

        var customUserResponse = response.Data.First(r => r.Id == organizationUsers.First().Id);
        Assert.Equal(OrganizationUserType.Custom, customUserResponse.Type);
        Assert.True(customUserResponse.Permissions.AccessReports);
        Assert.True(customUserResponse.Permissions.AccessEventLogs);
        Assert.False(customUserResponse.Permissions.EditAssignedCollections);
        Assert.False(customUserResponse.Permissions.DeleteAssignedCollections);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_WithFlexibleCollections_DowngradesCustomUsersWithDeprecatedPermissions(
        ICollection<OrganizationUserUserDetails> organizationUsers, OrganizationAbility organizationAbility,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        Get_Setup(organizationAbility, organizationUsers, sutProvider);

        var customUser = organizationUsers.First();
        customUser.Type = OrganizationUserType.Custom;
        customUser.Permissions = CoreHelpers.ClassToJsonData(new Permissions
        {
            EditAssignedCollections = true,
            DeleteAssignedCollections = true,
        });

        var response = await sutProvider.Sut.Get(organizationAbility.Id);

        var customUserResponse = response.Data.First(r => r.Id == organizationUsers.First().Id);
        Assert.Equal(OrganizationUserType.User, customUserResponse.Type);
        Assert.False(customUserResponse.Permissions.EditAssignedCollections);
        Assert.False(customUserResponse.Permissions.DeleteAssignedCollections);
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountRecoveryDetails_ReturnsDetails(
        Guid organizationId,
        OrganizationUserBulkRequestModel bulkRequestModel,
        ICollection<OrganizationUserResetPasswordDetails> resetPasswordDetails,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(true);
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAccountRecoveryDetailsByOrganizationUserAsync(organizationId, bulkRequestModel.Ids)
            .Returns(resetPasswordDetails);

        var response = await sutProvider.Sut.GetAccountRecoveryDetails(organizationId, bulkRequestModel);

        Assert.Equal(resetPasswordDetails.Count, response.Data.Count());
        Assert.True(response.Data.All(r =>
            resetPasswordDetails.Any(ou =>
                ou.OrganizationUserId == r.OrganizationUserId &&
                ou.Kdf == r.Kdf &&
                ou.KdfIterations == r.KdfIterations &&
                ou.KdfMemory == r.KdfMemory &&
                ou.KdfParallelism == r.KdfParallelism &&
                ou.ResetPasswordKey == r.ResetPasswordKey &&
                ou.EncryptedPrivateKey == r.EncryptedPrivateKey)));
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountRecoveryDetails_WithoutManageResetPasswordPermission_Throws(
        Guid organizationId,
        OrganizationUserBulkRequestModel bulkRequestModel,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        sutProvider.GetDependency<ICurrentContext>().ManageResetPassword(organizationId).Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.GetAccountRecoveryDetails(organizationId, bulkRequestModel));
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

    private void Get_Setup(OrganizationAbility organizationAbility,
        ICollection<OrganizationUserUserDetails> organizationUsers,
        SutProvider<OrganizationUsersController> sutProvider)
    {
        organizationAbility.FlexibleCollections = true;
        foreach (var orgUser in organizationUsers)
        {
            orgUser.Permissions = null;
        }
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organizationAbility.Id)
            .Returns(organizationAbility);

        sutProvider.GetDependency<IAuthorizationService>().AuthorizeAsync(
            user: Arg.Any<ClaimsPrincipal>(),
            resource: Arg.Any<Object>(),
            requirements: Arg.Any<IEnumerable<IAuthorizationRequirement>>())
            .Returns(AuthorizationResult.Success());

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync(organizationAbility.Id, Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(organizationUsers);
    }
}
