using System.Security.Claims;
using System.Text.Json;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Request;
using Bit.Api.Vault.Models.Response;
using Bit.Core;
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
    [BitAutoData(false)]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task CanEditCiphersAsAdminAsync_Providers(
        bool restrictProviders, CipherDetails cipherDetails, CurrentContextOrganization organization, Guid userId, SutProvider<CiphersController> sutProvider
    )
    {
        cipherDetails.OrganizationId = organization.Id;

        // Simulate that the user is a provider for the organization
        sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(restrictProviders);

        // Non restricted providers should succeed
        if (!restrictProviders)
        {
            await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);
            await sutProvider.GetDependency<ICipherService>().ReceivedWithAnyArgs()
                .DeleteAsync(default, default);
        }
        else // Otherwise, they should fail
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipherDetails.Id));
            await sutProvider.GetDependency<ICipherService>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default, default);
        }

        await sutProvider.GetDependency<ICurrentContext>().Received().ProviderUserForOrgAsync(organization.Id);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithOwnerOrAdmin_WithEditPermission_DeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithOwnerOrAdmin_WithoutEditPermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = false;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipherDetails.Id));

        await sutProvider.GetDependency<ICipherService>().DidNotReceive().DeleteAsync(Arg.Any<CipherDetails>(), Arg.Any<Guid>(), Arg.Any<bool>());
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteAdmin_WithLimitItemDeletionEnabled_WithOwnerOrAdmin_WithManagePermission_DeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.LimitItemDeletion).Returns(true);
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
    public async Task DeleteAdmin_WithLimitItemDeletion_WithOwnerOrAdmin_WithoutManagePermission_ThrowsNotFoundException(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;
        cipherDetails.Manage = false;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.LimitItemDeletion).Returns(true);
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
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails> { new() { Id = cipherDetails.Id } });

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
    public async Task DeleteAdmin_WithProviderUser_DeletesCipher(
        CipherDetails cipherDetails, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipherDetails.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(cipherDetails.OrganizationId.Value).Returns(new List<Cipher> { cipherDetails });

        await sutProvider.Sut.DeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipher.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithEditPermission_DeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true
            }).ToList());

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
    public async Task DeleteManyAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCiphers_DeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(ciphers.Select(c => new CipherOrganizationDetails { Id = c.Id }).ToList());

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
    public async Task DeleteManyAdmin_WithProviderUser_DeletesCiphers(
        CipherBulkDeleteRequestModel model, Guid userId,
        List<Cipher> ciphers, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organizationId).Returns(ciphers);

        await sutProvider.Sut.DeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .DeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organizationId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();

        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithEditPermission_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Edit = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCipher_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);

        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails> { new() { Id = cipherDetails.Id } });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_SoftDeletesCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithCustomUser_WithEditAnyCollectionTrue_SoftDeletesCipher(
        CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(cipherDetails, userId, true);
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
    public async Task PutDeleteAdmin_WithProviderUser_SoftDeletesCipher(
        CipherDetails cipherDetails, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipherDetails.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(cipherDetails.OrganizationId.Value).Returns(new List<Cipher> { cipherDetails });

        await sutProvider.Sut.PutDeleteAdmin(cipherDetails.Id);

        await sutProvider.GetDependency<ICipherService>().Received(1).SoftDeleteAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipher.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipher.Id));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithEditPermission_SoftDeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true
            }).ToList());

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
    public async Task PutDeleteManyAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCiphers_SoftDeletesCiphers(
        OrganizationUserType organizationUserType, CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(ciphers.Select(c => new CipherOrganizationDetails { Id = c.Id }).ToList());

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

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
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

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
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
    public async Task PutDeleteManyAdmin_WithProviderUser_SoftDeletesCiphers(
        CipherBulkDeleteRequestModel model, Guid userId,
        List<Cipher> ciphers, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organizationId).Returns(ciphers);

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count() == model.Ids.Count()),
                userId, organizationId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        var organizationId = Guid.NewGuid();
        model.OrganizationId = organizationId.ToString();

        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organizationId).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithEditPermission_RestoresCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.UserId = null;
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        cipherDetails.Edit = true;

        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(new List<CipherDetails>
            {
                cipherDetails
            });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.NotNull(result);
        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithAccessToUnassignedCipher_RestoresCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyUnassignedOrganizationDetailsByOrganizationIdAsync(organization.Id)
            .Returns(new List<CipherOrganizationDetails> { new() { Id = cipherDetails.Id } });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.NotNull(result);
        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreAdmin_WithOwnerOrAdmin_WithAccessToAllCollectionItems_RestoresCipher(
        OrganizationUserType organizationUserType, CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.NotNull(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithCustomUser_WithEditAnyCollectionTrue_RestoresCipher(
        CipherDetails cipherDetails, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = organization.Id;
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());
        organization.Type = OrganizationUserType.Custom;
        organization.Permissions.EditAnyCollection = true;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipherDetails });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.NotNull(result);
        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(cipherDetails, userId, true);
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
    public async Task PutRestoreAdmin_WithProviderUser_RestoresCipher(
        CipherDetails cipherDetails, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();
        cipherDetails.Type = CipherType.Login;
        cipherDetails.Data = JsonSerializer.Serialize(new CipherLoginData());

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipherDetails.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherDetails.Id, userId).Returns(cipherDetails);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(cipherDetails.OrganizationId.Value).Returns(new List<Cipher> { cipherDetails });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id);

        Assert.NotNull(result);
        Assert.IsType<CipherMiniResponseModel>(result);
        await sutProvider.GetDependency<ICipherService>().Received(1).RestoreAsync(cipherDetails, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        CipherDetails cipherDetails, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipherDetails.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(cipherDetails.OrganizationId.Value).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipherDetails.Id).Returns(cipherDetails);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipherDetails.Id));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task PutRestoreManyAdmin_WithOwnerOrAdmin_WithEditPermission_RestoresCiphers(
        OrganizationUserType organizationUserType, CipherBulkRestoreRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();
        organization.Type = organizationUserType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<ICipherRepository>()
            .GetManyByUserIdAsync(userId)
            .Returns(ciphers.Select(c => new CipherDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Edit = true
            }).ToList());

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = organization.Id
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.NotNull(result);
        await sutProvider.GetDependency<ICipherService>().Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, organization.Id, true);
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
                ids.All(id => model.Ids.Contains(id.ToString()) && ids.Count == model.Ids.Count())),
                userId, organization.Id, true)
            .Returns(cipherOrgDetails);

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
    public async Task PutRestoreManyAdmin_WithProviderUser_RestoresCiphers(
        CipherBulkRestoreRequestModel model, Guid userId,
        List<Cipher> ciphers, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = Guid.NewGuid();
        model.Ids = ciphers.Select(c => c.Id.ToString()).ToList();

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(model.OrganizationId).Returns(true);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(model.OrganizationId).Returns(ciphers);

        var cipherOrgDetails = ciphers.Select(c => new CipherOrganizationDetails
        {
            Id = c.Id,
            OrganizationId = model.OrganizationId
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(
                Arg.Any<HashSet<Guid>>(),
                userId, model.OrganizationId, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        Assert.NotNull(result);
        await sutProvider.GetDependency<ICipherService>()
            .Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => model.Ids.Contains(id.ToString())) && ids.Count == model.Ids.Count()),
                userId, model.OrganizationId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithProviderUser_WithRestrictProviderAccessTrue_ThrowsNotFoundException(
        CipherBulkRestoreRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = Guid.NewGuid();

        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(model.OrganizationId).Returns(true);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(true);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreManyAdmin(model));
    }
}
