using System.Security.Claims;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models.Request;
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
    public async Task PutPartialShouldReturnCipherWithGivenFolderAndFavoriteValues(Guid userId, Guid folderId, SutProvider<CiphersController> sutProvider)
    {
        var isFavorite = true;
        var cipherId = Guid.NewGuid();

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);

        var cipherDetails = new CipherDetails
        {
            Favorite = isFavorite,
            FolderId = folderId,
            Type = Core.Vault.Enums.CipherType.SecureNote,
            Data = "{}"
        };

        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(Task.FromResult(cipherDetails));

        var result = await sutProvider.Sut.PutPartial(cipherId, new CipherPartialRequestModel { Favorite = isFavorite, FolderId = folderId.ToString() });

        Assert.Equal(folderId, result.FolderId);
        Assert.Equal(isFavorite, result.Favorite);
    }

    [Theory, BitAutoData]
    public async Task PutCollections_vNextShouldThrowExceptionWhenCipherIsNullOrNoOrgValue(Guid id, CipherCollectionsRequestModel model, Guid userId,
        SutProvider<CiphersController> sutProvider)
    {
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().OrganizationUser(Guid.NewGuid()).Returns(false);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(id, userId).ReturnsNull();

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
}
