using System.Security.Claims;
using System.Text.Json;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;
using CipherType = Bit.Core.Vault.Enums.CipherType;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(CiphersController))]
[SutProviderCustomize]
public class CiphersControllerTests
{
    [Theory, BitAutoData]
    public async Task PutPartialShouldReturnCipherWithGivenFolderAndFavoriteValues(User user, Guid folderId, SutProvider<CiphersController> sutProvider)
    {
        var isFavorite = true;
        var cipherId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);

        var cipherDetails = new CipherDetails
        {
            UserId = user.Id,
            Favorite = isFavorite,
            FolderId = folderId,
            Type = Core.Vault.Enums.CipherType.SecureNote,
            Data = "{}"
        };

        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, user.Id)
            .Returns(Task.FromResult(cipherDetails));

        var result = await sutProvider.Sut.PutPartial(cipherId, new CipherPartialRequestModel { Favorite = isFavorite, FolderId = folderId.ToString() });

        Assert.Equal(folderId, result.FolderId);
        Assert.Equal(isFavorite, result.Favorite);
    }

    [Theory, BitAutoData]
    public async Task PutCollections_vNextShouldThrowExceptionWhenCipherIsNullOrNoOrgValue(Guid id, CipherCollectionsRequestModel model, User user,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(user);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(Guid.NewGuid()).Returns(false);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(id, user.Id).ReturnsNull();

        var requestAction = async () => await sutProvider.Sut.PutCollections_vNext(id, model);

        await Assert.ThrowsAsync<NotFoundException>(requestAction);
    }

    [Theory, BitAutoData]
    public async Task PutCollections_vNextShouldSaveUpdatedCipher(Guid id, CipherCollectionsRequestModel model, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        SetupUserAndOrgMocks(id, userId, sutProvider);
        var cipherDetails = CreateCipherDetailsMock(id, userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(id, userId).ReturnsForAnyArgs(cipherDetails);

        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdCipherIdAsync(userId, id).Returns((ICollection<CollectionCipher>)new List<CollectionCipher>());
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(new Dictionary<Guid, OrganizationAbility> { { cipherDetails.OrganizationId.Value, new OrganizationAbility() } });
        var cipherService = sutProvider.GetDependency<ICipherService>();

        await sutProvider.Sut.PutCollections_vNext(id, model);

        await cipherService.ReceivedWithAnyArgs().SaveCollectionsAsync(default, default, default, default);
    }

    [Theory, BitAutoData]
    public async Task PutCollections_vNextReturnOptionalDetailsCipherUnavailableFalse(Guid id, CipherCollectionsRequestModel model, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        SetupUserAndOrgMocks(id, userId, sutProvider);
        var cipherDetails = CreateCipherDetailsMock(id, userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(id, userId).ReturnsForAnyArgs(cipherDetails);

        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdCipherIdAsync(userId, id).Returns((ICollection<CollectionCipher>)new List<CollectionCipher>());
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilitiesAsync().Returns(new Dictionary<Guid, OrganizationAbility> { { cipherDetails.OrganizationId.Value, new OrganizationAbility() } });

        var result = await sutProvider.Sut.PutCollections_vNext(id, model);

        Assert.IsType<OptionalCipherDetailsResponseModel>(result);
        Assert.False(result.Unavailable);
    }

    [Theory, BitAutoData]
    public async Task PutCollections_vNextReturnOptionalDetailsCipherUnavailableTrue(Guid id, CipherCollectionsRequestModel model, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        SetupUserAndOrgMocks(id, userId, sutProvider);
        var cipherDetails = CreateCipherDetailsMock(id, userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(id, userId).ReturnsForAnyArgs(cipherDetails, [(CipherDetails)null]);

        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdCipherIdAsync(userId, id).Returns((ICollection<CollectionCipher>)new List<CollectionCipher>());

        var result = await sutProvider.Sut.PutCollections_vNext(id, model);

        Assert.IsType<OptionalCipherDetailsResponseModel>(result);
        Assert.True(result.Unavailable);
    }

    private void SetupUserAndOrgMocks(Guid id, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(default).ReturnsForAnyArgs(true);
        sutProvider.GetDependency<ICollectionCipherRepository>().GetManyByUserIdCipherIdAsync(userId, id).Returns(new List<CollectionCipher>());
    }

    private CipherDetails CreateCipherDetailsMock(Guid id, Guid userId)
    {
        return new CipherDetails
        {
            Id = id,
            UserId = userId,
            OrganizationId = Guid.NewGuid(),
            Type = CipherType.Login,
            ViewPassword = true,
            Data = @"
            {
                ""Uris"": [
                    {
                        ""Uri"": ""https://bitwarden.com""
                    }
                ],
                ""Username"": ""testuser"",
                ""Password"": ""securepassword123""
            }"
        };
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Admin, true, true)]
    [BitAutoData(OrganizationUserType.Owner, true, true)]
    [BitAutoData(OrganizationUserType.Custom, false, true)]
    [BitAutoData(OrganizationUserType.Custom, true, true)]
    [BitAutoData(OrganizationUserType.Admin, false, false)]
    [BitAutoData(OrganizationUserType.Owner, false, false)]
    [BitAutoData(OrganizationUserType.Custom, false, false)]
    public async Task CanEditCiphersAsAdminAsync_FlexibleCollections_Success(
        OrganizationUserType userType, bool allowAdminsAccessToAllItems, bool shouldSucceed,
        CurrentContextOrganization organization, Guid userId, CipherDetails cipherDetails, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = userType;
        if (userType == OrganizationUserType.Custom)
        {
            // Assume custom users have EditAnyCollections for success case
            organization.Permissions.EditAnyCollection = shouldSucceed;
        }
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });

        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);

        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = allowAdminsAccessToAllItems
        });

        if (shouldSucceed)
        {
            await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);
            await sutProvider.GetDependency<ICipherService>().ReceivedWithAnyArgs()
                .DeleteAsync(default, default);
        }
        else
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipherDetails.Id));
            await sutProvider.GetDependency<ICipherService>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default, default);
        }
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithOwnerOrAdmin_WithManagePermission_DeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipherDetails.Id));

        await sutProvider.GetDependency<ICipherService>().DidNotReceive().DeleteAsync(Arg.Any<CipherDetails>(), Arg.Any<Guid>(), Arg.Any<bool>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCipher_DeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails>
            {
                new() { Id = cipherDetails.Id, OrganizationId = cipherDetails.OrganizationId }
            });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithAdminOrOwner_WithAccessToAllCollectionItems_DeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithCustomUser_WithEditAnyCollectionTrue_DeletesCipher(
        CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithProviderUser_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipher.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id));
    }





    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithManagePermission_DeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = true
            }).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.DeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .DeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = false
            }).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCiphers_DeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(ciphers.Select(c => new CipherOrganizationDetails { Id = c.Id, OrganizationId = organization.Id }).ToList());
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.DeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .DeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_DeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.DeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .DeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithCustomUser_WithEditAnyCollectionTrue_DeletesCiphers(
        CipherBulkDeleteRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);

        await sutProvider.Sut.DeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .DeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithProviderUser_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();

        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }





    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithManagePermission_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.UserId = null;
        cipherOrgDetails.OrganizationId = organization.Id;

        var cipherDetails = new CipherDetails(cipherOrgDetails);
        cipherDetails.Edit = true;
        cipherDetails.Manage = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(
                Arg.Is<CipherDetails>(c => c.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id));

        await sutProvider.GetDependency<ICipherService>()
            .DidNotReceiveWithAnyArgs()
            .SoftDeleteManyAsync(default, default, default, default);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCipher_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails> { new() { Id = cipherOrgDetails.Id, OrganizationId = organization.Id } });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.PutDeleteAdmin(cipherOrgDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(
                Arg.Is<CipherDetails>(c => c.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherOrgDetails });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.PutDeleteAdmin(cipherOrgDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(
                Arg.Is<CipherDetails>(c => c.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithCustomUser_WithEditAnyCollectionTrue_SoftDeletesCipher(
        CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherOrgDetails });

        await sutProvider.Sut.PutDeleteAdmin(cipherOrgDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(
                Arg.Is<CipherDetails>(c => c.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithEditPermission_WithLimitItemDeletionFalse_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.UserId = null;
        cipherOrgDetails.OrganizationId = organization.Id;

        var cipherDetails = new CipherDetails(cipherOrgDetails);
        cipherDetails.Edit = true;
        cipherDetails.Manage = false; // Only Edit permission, not Manage

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = false
            });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(
                Arg.Is<CipherDetails>(c => c.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithEditPermission_WithLimitItemDeletionTrue_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false; // Only Edit permission, not Manage
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipher.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithProviderUser_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipher.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipher.Id));
    }





    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithManagePermission_SoftDeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = true
            }).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = false
            }).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCiphers_SoftDeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(ciphers.Select(c => new CipherOrganizationDetails { Id = c.Id, OrganizationId = organization.Id }).ToList());
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_SoftDeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        // Set organization ID on ciphers to avoid "Cipher needs to belong to a user or an organization" error
        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithCustomUser_WithEditAnyCollectionTrue_SoftDeletesCiphers(
        CipherBulkDeleteRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        // Set organization ID on ciphers to avoid "Cipher needs to belong to a user or an organization" error
        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithProviderUser_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();

        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }





    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithManagePermission_RestoresCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.UserId = null;
        cipherOrgDetails.OrganizationId = organization.Id;
        cipherOrgDetails.Type = CipherType.Login;
        cipherOrgDetails.Data = JsonSerializer.Serialize(new CipherLoginData());

        var cipherDetails = new CipherDetails(cipherOrgDetails);
        cipherDetails.Edit = true;
        cipherDetails.Manage = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherOrgDetails.Id);

        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(Arg.Is<CipherDetails>(
                    (cd) => cd.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.UserId = null;
        cipherOrgDetails.OrganizationId = organization.Id;

        var cipherDetails = new CipherDetails(cipherOrgDetails);
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCipher_RestoresCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        cipherOrgDetails.Type = CipherType.Login;
        cipherOrgDetails.Data = JsonSerializer.Serialize(new CipherLoginData());

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails> { new() { Id = cipherOrgDetails.Id, OrganizationId = organization.Id } });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherOrgDetails.Id);

        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(Arg.Is<CipherDetails>(
                    (cd) => cd.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_RestoresCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        cipherOrgDetails.Type = CipherType.Login;
        cipherOrgDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherOrgDetails });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherOrgDetails.Id);

        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(Arg.Is<CipherDetails>(
                    (cd) => cd.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithCustomUser_WithEditAnyCollectionTrue_RestoresCipher(
        CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.OrganizationId = organization.Id;
        cipherOrgDetails.Type = CipherType.Login;
        cipherOrgDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherOrgDetails.Id).Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherOrgDetails });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherOrgDetails.Id);

        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(Arg.Is<CipherDetails>(
                    (cd) => cd.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithEditPermission_LimitItemDeletionFalse_RestoresCipher(
        OrganizationUserType organizationUserType, CipherOrganizationDetails cipherOrgDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherOrgDetails.UserId = null;
        cipherOrgDetails.OrganizationId = organization.Id;
        cipherOrgDetails.Type = CipherType.Login;
        cipherOrgDetails.Data = JsonSerializer.Serialize(new CipherLoginData());

        var cipherDetails = new CipherDetails(cipherOrgDetails);
        cipherDetails.Edit = true;
        cipherDetails.Manage = false; // Only Edit permission, not Manage

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = false // Permissive mode - Edit permission should work
            });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(Arg.Is<CipherDetails>(
                    (cd) => cd.OrganizationId.Equals(cipherOrgDetails.OrganizationId)), userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithEditPermission_LimitItemDeletionTrue_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        cipherDetails.Edit = true;
        cipherDetails.Manage = false; // Only Edit permission, not Manage
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true // Restrictive mode - Edit permission should NOT work
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithProviderUser_ThrowsNotFoundException(
        CipherDetails cipherDetails, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipherDetails.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreManyAdmin_WithOwnerOrAdmin_WithManagePermission_RestoresCiphers(
        OrganizationUserType organizationUserType, CipherBulkRestoreRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = true
            }).ToList());

        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = organization.Id,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData())
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.Equal(ciphers.Count, result.Data.Count());
        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreManyAdmin_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherBulkRestoreRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true,
                Manage = false,
                Type = CipherType.Login,
                Data = JsonSerializer.Serialize(new CipherLoginData())
            }).ToList());
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreManyAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCiphers_RestoresCiphers(
        OrganizationUserType organizationUserType, CipherBulkRestoreRequestModel model, Guid userId,
        List<Cipher> ciphers, CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<IUserService>().GetUserByPrincipalAsync(default).ReturnsForAnyArgs(new User { Id = userId });
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = organization.Id,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData())
        }).ToList();

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(cipherOrgDetails);
        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(Arg.Is<HashSet<Guid>>(ids =>
                ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organization.Id, true)
            .Returns(cipherOrgDetails);
        sutProvider.GetDependency<IApplicationCacheService>()
            .GetOrganizationAbilityAsync(organization.Id)
            .Returns(new OrganizationAbility
            {
                Id = organization.Id,
                LimitItemDeletion = true
            });

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.NotNull(result);
        Assert.Equal(model.Ids.Count(), result.Data.Count());
        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreManyAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_RestoresCiphers(
        OrganizationUserType organizationUserType, CipherBulkRestoreRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = organization.Id,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData())
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(Arg.Any<HashSet<Guid>>(), userId, organization.Id, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.NotNull(result);
        Assert.Equal(ciphers.Count, result.Data.Count());
        await sutProvider.GetDependency<ICipherService>().Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithCustomUser_WithEditAnyCollectionTrue_RestoresCiphers(
        CipherBulkRestoreRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = organization.Id,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData())
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.NotNull(result);
        Assert.Equal(ciphers.Count, result.Data.Count());
        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithCustomUser_WithEditAnyCollectionFalse_ThrowsNotFoundException(
        CipherBulkRestoreRequestModel model,
        Guid userId, List<Cipher> ciphers, CurrentContextOrganization organization,
        SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = false;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreManyAdmin(model));
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithProviderUser_ThrowsNotFoundException(
        CipherBulkRestoreRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>()
            .ProviderUserForOrgAsync(new Guid(model.OrganizationId.ToString()))
            .Returns(Task.FromResult(true));

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PutRestoreManyAdmin(model)
        );
    }

    [Theory]
    [BitAutoData]
    public async Task PutShareMany_ShouldShareCiphersAndReturnRevisionDateMap(
        User user,
        Guid organizationId,
        Guid userId,
        SutProvider<CiphersController> sutProvider)
    {
        var oldDate1 = DateTime.UtcNow.AddDays(-1);
        var oldDate2 = DateTime.UtcNow.AddDays(-2);
        var detail1 = new CipherDetails
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData()),
            RevisionDate = oldDate1
        };
        var detail2 = new CipherDetails
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = CipherType.Login,
            Data = JsonSerializer.Serialize(new CipherLoginData()),
            RevisionDate = oldDate2
        };
        var preloadedDetails = new List<CipherDetails> { detail1, detail2 };

        var newDate1 = oldDate1.AddMinutes(5);
        var newDate2 = oldDate2.AddMinutes(5);
        var updatedCipher1 = new CipherDetails { Id = detail1.Id, RevisionDate = newDate1, Type = detail1.Type, Data = detail1.Data };
        var updatedCipher2 = new CipherDetails { Id = detail2.Id, RevisionDate = newDate2, Type = detail2.Type, Data = detail2.Data };

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationUser(organizationId)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(Task.FromResult(user));
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(default!)
            .ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId, withOrganizations: false)
            .Returns(Task.FromResult((ICollection<CipherDetails>)preloadedDetails));

        sutProvider.GetDependency<ICipherService>()
            .ShareManyAsync(
                Arg.Any<IEnumerable<(CipherDetails, DateTime?)>>(),
                organizationId,
                Arg.Any<IEnumerable<Guid>>(),
                userId
            )
            .Returns(Task.FromResult<IEnumerable<CipherDetails>>(new[] { updatedCipher1, updatedCipher2 }));

        var cipherRequests = preloadedDetails.Select(d =>
        {
            var m = new CipherWithIdRequestModel
            {
                Id = d.Id,
                OrganizationId = d.OrganizationId!.Value.ToString(),
                LastKnownRevisionDate = d.RevisionDate,
                Type = d.Type,
            };

            if (d.Type == CipherType.Login)
            {
                m.Login = new CipherLoginModel
                {
                    Username = "",
                    Password = "",
                    Uris = [],
                };
                m.Name = "";
                m.Notes = "";
                m.Fields = Array.Empty<CipherFieldModel>();
                m.PasswordHistory = Array.Empty<CipherPasswordHistoryModel>();
            }

            // similar for SecureNote, Card, etc., if you ever hit those branches
            return m;
        }).ToList();

        var model = new CipherBulkShareRequestModel
        {
            Ciphers = cipherRequests,
            CollectionIds = new[] { Guid.NewGuid().ToString() }
        };

        var result = await sutProvider.Sut.PutShareMany(model);

        Assert.Equal(2, result.Data.Count());
        var revisionDates = result.Data.Select(x => x.RevisionDate).ToList();
        Assert.Contains(newDate1, revisionDates);
        Assert.Contains(newDate2, revisionDates);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .ShareManyAsync(
                Arg.Is<IEnumerable<(CipherDetails, DateTime?)>>(list =>
                    list.Select(x => x.Item1.Id).OrderBy(id => id)
                        .SequenceEqual(new[] { detail1.Id, detail2.Id }.OrderBy(id => id))
                ),
                organizationId,
                Arg.Any<IEnumerable<Guid>>(),
                userId
            );
    }

    [Theory, BitAutoData]
    public async Task PutShareMany_OrganizationUserFalse_ThrowsNotFound(
        CipherBulkShareRequestModel model,
        SutProvider<CiphersController> sut)
    {
        model.Ciphers = new[] {
          new CipherWithIdRequestModel { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid().ToString() }
        };
        sut.GetDependency<ICurrentContext>()
            .OrganizationUser(Arg.Any<Guid>())
            .Returns(Task.FromResult(false));

        await Assert.ThrowsAsync<NotFoundException>(() => sut.Sut.PutShareMany(model));
    }
    [Theory, BitAutoData]
    public async Task PutShareMany_CipherNotOwned_ThrowsNotFoundException(
        Guid organizationId,
        Guid userId,
        CipherWithIdRequestModel request,
        SutProvider<CiphersController> sutProvider)
    {
        request.EncryptedFor = userId;
        var model = new CipherBulkShareRequestModel
        {
            Ciphers = new[] { request },
            CollectionIds = new[] { Guid.NewGuid().ToString() }
        };

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationUser(organizationId)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(default)
            .ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId, withOrganizations: false)
            .Returns(Task.FromResult((ICollection<CipherDetails>)new List<CipherDetails>()));

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PutShareMany(model)
        );
    }

    [Theory, BitAutoData]
    public async Task PutShareMany_EncryptedForWrongUser_ThrowsNotFoundException(
        Guid organizationId,
        Guid userId,
        CipherWithIdRequestModel request,
        SutProvider<CiphersController> sutProvider)
    {
        request.EncryptedFor = Guid.NewGuid(); // not equal to userId
        var model = new CipherBulkShareRequestModel
        {
            Ciphers = new[] { request },
            CollectionIds = new[] { Guid.NewGuid().ToString() }
        };

        sutProvider.GetDependency<ICurrentContext>()
            .OrganizationUser(organizationId)
            .Returns(Task.FromResult(true));
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(default)
            .ReturnsForAnyArgs(userId);

        var existing = new CipherDetails { Id = request.Id.Value };
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId, withOrganizations: false)
            .Returns(Task.FromResult((ICollection<CipherDetails>)(new[] { existing })));

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.PutShareMany(model)
        );
    }

    [Theory, BitAutoData]
    public async Task PostPurge_WhenUserNotFound_ThrowsUnauthorizedAccessException(
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns((User)null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => sutProvider.Sut.PostPurge(model));
    }

    [Theory, BitAutoData]
    public async Task PostPurge_WhenUserVerificationFails_ThrowsBadRequestException(
        User user,
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(user, model.Secret)
            .Returns(false);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostPurge(model));
    }

    [Theory, BitAutoData]
    public async Task PostPurge_WhenUserIsClaimedByAnOrganization_ThrowsBadRequestException(
        User user,
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(user, model.Secret)
            .Returns(true);
        sutProvider.GetDependency<IUserService>()
            .IsClaimedByAnyOrganizationAsync(user.Id)
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.PostPurge(model));
    }

    [Theory, BitAutoData]
    public async Task PostPurge_UserPurge_Successful(
        User user,
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(user, model.Secret)
            .Returns(true);
        sutProvider.GetDependency<IUserService>()
            .IsClaimedByAnyOrganizationAsync(user.Id)
            .Returns(false);

        await sutProvider.Sut.PostPurge(model);

        await sutProvider.GetDependency<ICipherRepository>()
            .Received(1)
            .DeleteByUserIdAsync(user.Id);
    }

    [Theory, BitAutoData]
    public async Task PostPurge_OrganizationPurge_Successful(
        User user,
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        var orgId = Guid.NewGuid();
        var orgIdStr = orgId.ToString();
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(user, model.Secret)
            .Returns(true);
        sutProvider.GetDependency<IUserService>()
            .IsClaimedByAnyOrganizationAsync(user.Id)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>()
            .EditAnyCollection(orgId)
            .Returns(true);

        await sutProvider.Sut.PostPurge(model, orgIdStr);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .PurgeAsync(orgId);
    }

    [Theory, BitAutoData]
    public async Task PostPurge_OrganizationPurge_WithInsufficientPermissions_ThrowsNotFoundException(
        User user,
        SecretVerificationRequestModel model,
        SutProvider<CiphersController> sutProvider)
    {
        var orgId = Guid.NewGuid();
        var orgIdStr = orgId.ToString();
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(Arg.Any<ClaimsPrincipal>())
            .Returns(user);
        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(user, model.Secret)
            .Returns(true);
        sutProvider.GetDependency<IUserService>()
            .IsClaimedByAnyOrganizationAsync(user.Id)
            .Returns(false);
        sutProvider.GetDependency<ICurrentContext>()
            .EditAnyCollection(orgId)
            .Returns(false);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostPurge(model, orgIdStr));
    }
}

