using System.Security.Claims;
using System.Text.Json;
using Bit.Api.Models.Response;
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
        CurrentContextOrganization organization, Guid userId, Cipher cipher, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;
        organization.Type = userType;
        if (userType == OrganizationUserType.Custom)
        {
            // Assume custom users have EditAnyCollections for success case
            organization.Permissions.EditAnyCollection = shouldSucceed;
        }
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = allowAdminsAccessToAllItems
        });

        if (shouldSucceed)
        {
            await sutProvider.Sut.DeleteAdmin(cipher.Id.ToString());
            await sutProvider.GetDependency<ICipherService>().ReceivedWithAnyArgs()
                .DeleteAsync(default, default);
        }
        else
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id.ToString()));
            await sutProvider.GetDependency<ICipherService>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default, default);
        }
    }

    [Theory]
    [BitAutoData(false)]
    [BitAutoData(false)]
    [BitAutoData(true)]
    public async Task CanEditCiphersAsAdminAsync_Providers(
        bool restrictProviders, Cipher cipher, CurrentContextOrganization organization, Guid userId, SutProvider<CiphersController> sutProvider
    )
    {
        cipher.OrganizationId = organization.Id;

        // Simulate that the user is a provider for the organization
        sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(true);

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(restrictProviders);

        // Non restricted providers should succeed
        if (!restrictProviders)
        {
            await sutProvider.Sut.DeleteAdmin(cipher.Id.ToString());
            await sutProvider.GetDependency<ICipherService>().ReceivedWithAnyArgs()
                .DeleteAsync(default, default);
        }
        else // Otherwise, they should fail
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id.ToString()));
            await sutProvider.GetDependency<ICipherService>().DidNotReceiveWithAnyArgs()
                .DeleteAsync(default, default);
        }

        await sutProvider.GetDependency<ICurrentContext>().Received().ProviderUserForOrgAsync(organization.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithValidOrganizationAndPermissions_DeletesCipher(
        Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;

        organization.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.DeleteAdmin(cipher.Id.ToString());

        await sutProvider.GetDependency<ICipherService>().Received(1).DeleteAsync(cipher, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteAdmin_WithoutOrganizationId_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task DeleteAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithValidOrganizationAndPermissions_DeletesCiphers(
        CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        var cipherIds = ciphers.Select(c => c.Id).ToList();
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        organization.Type = OrganizationUserType.Owner;

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
                    ids.All(id => cipherIds.Contains(id)) && ids.Count() == cipherIds.Count),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyAdmin_WithInvalidOrganizationId_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = null;

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task DeleteManyAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, CipherBulkDeleteRequestModel model, Guid userId, List<Guid> cipherIds,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.DeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithValidOrganizationAndPermissions_SoftDeletesCipher(
        Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;

        organization.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.PutDeleteAdmin(cipher.Id.ToString());

        await sutProvider.GetDependency<ICipherService>().Received(1)
            .SoftDeleteAsync(cipher, userId, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteAdmin_WithoutOrganizationId_ThrowsNotFoundException(
        Cipher cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task PutDeleteAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, Cipher cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithValidOrganizationAndPermissions_SoftDeletesCiphers(
        CipherBulkDeleteRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        var cipherIds = ciphers.Select(c => c.Id).ToList();
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        organization.Type = OrganizationUserType.Owner;

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        await sutProvider.Sut.PutDeleteManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>().Received(1)
            .SoftDeleteManyAsync(
                Arg.Is<IEnumerable<Guid>>(ids =>
                    ids.All(id => cipherIds.Contains(id)) && ids.Count() == cipherIds.Count),
                userId, organization.Id, true);
    }

    [Theory]
    [BitAutoData]
    public async Task PutDeleteManyAdmin_WithInvalidOrganizationId_ThrowsNotFoundException(
        CipherBulkDeleteRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = null;

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task PutDeleteManyAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, CipherBulkDeleteRequestModel model, Guid userId, List<Guid> cipherIds,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id.ToString();
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutDeleteManyAdmin(model));
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithValidOrganizationAndPermissions_RestoresCipher(
        CipherDetails cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;
        cipher.Type = CipherType.Login;
        cipher.Data = JsonSerializer.Serialize(new CipherLoginData());

        organization.Type = OrganizationUserType.Owner;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        var result = await sutProvider.Sut.PutRestoreAdmin(cipher.Id.ToString());

        await sutProvider.GetDependency<ICipherService>().Received(1)
            .RestoreAsync(cipher, userId, true);
        Assert.NotNull(result);
        Assert.IsType<CipherMiniResponseModel>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreAdmin_WithoutOrganizationId_ThrowsNotFoundException(
        CipherDetails cipher, Guid userId, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = null;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipher.Id).Returns(cipher);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task PutRestoreAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, CipherDetails cipher, Guid userId,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        cipher.OrganizationId = organization.Id;

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICipherRepository>().GetOrganizationDetailsByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreAdmin(cipher.Id.ToString()));
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithValidOrganizationAndPermissions_RestoresCiphers(
        CipherBulkRestoreRequestModel model, Guid userId, List<Cipher> ciphers,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        var cipherIds = ciphers.Select(c => c.Id).ToList();
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        organization.Type = OrganizationUserType.Owner;

        foreach (var cipher in ciphers)
        {
            cipher.OrganizationId = organization.Id;
            cipher.Type = CipherType.Login;
            cipher.Data = JsonSerializer.Serialize(new CipherLoginData());
        }

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(ciphers);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = true
        });

        var cipherOrgDetails = ciphers.Select(c =>
        {
            var details = new CipherOrganizationDetails
            {
                Id = c.Id,
                OrganizationId = organization.Id,
                Type = c.Type,
                Data = c.Data,
                Attachments = c.Attachments,
                RevisionDate = c.RevisionDate,
                DeletedDate = c.DeletedDate
            };
            return details;
        }).ToList();

        sutProvider.GetDependency<ICipherService>()
            .RestoreManyAsync(Arg.Any<HashSet<Guid>>(), userId, organization.Id, true)
            .Returns(cipherOrgDetails);

        var result = await sutProvider.Sut.PutRestoreManyAdmin(model);

        await sutProvider.GetDependency<ICipherService>().Received(1)
            .RestoreManyAsync(
                Arg.Is<HashSet<Guid>>(ids =>
                    ids.All(id => cipherIds.Contains(id)) && ids.Count == cipherIds.Count),
                userId, organization.Id, true);
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<CipherMiniResponseModel>>(result);
        Assert.Equal(ciphers.Count, result.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async Task PutRestoreManyAdmin_WithInvalidOrganizationId_ThrowsNotFoundException(
        CipherBulkRestoreRequestModel model, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = default;

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreManyAdmin(model));
    }

    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task PutRestoreManyAdmin_WithoutEditPermissions_ThrowsNotFoundException(
        OrganizationUserType userType, CipherBulkRestoreRequestModel model, Guid userId, List<Guid> cipherIds,
        CurrentContextOrganization organization, SutProvider<CiphersController> sutProvider)
    {
        model.OrganizationId = organization.Id;
        model.Ids = cipherIds.Select(id => id.ToString()).ToList();

        organization.Type = userType;

        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            AllowAdminAccessToAllCollectionItems = false
        });

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PutRestoreManyAdmin(model));
    }
}
