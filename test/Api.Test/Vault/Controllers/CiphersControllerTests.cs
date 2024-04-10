using System.Security.Claims;
using Bit.Api.Vault.Controllers;
using Bit.Api.Vault.Models;
using Bit.Api.Vault.Models.Request;
using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Enums;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Core.Vault.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

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
            .GetByIdAsync(cipherId, userId, Arg.Any<bool>())
            .Returns(Task.FromResult(cipherDetails));

        var result = await sutProvider.Sut.PutPartial(cipherId, new CipherPartialRequestModel { Favorite = isFavorite, FolderId = folderId.ToString() });

        Assert.Equal(folderId, result.FolderId);
        Assert.Equal(isFavorite, result.Favorite);
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
        CurrentContextOrganization organization, Guid userId, SutProvider<CiphersController> sutProvider
    )
    {
        organization.Type = userType;
        if (userType == OrganizationUserType.Custom)
        {
            // Assume custom users have EditAnyCollections for success case
            organization.Permissions.EditAnyCollection = shouldSucceed;
        }
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            FlexibleCollections = true,
            AllowAdminAccessToAllCollectionItems = allowAdminsAccessToAllItems
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(true);

        var requestModel = new CipherCreateRequestModel
        {
            Cipher = new CipherRequestModel { OrganizationId = organization.Id.ToString(), Type = CipherType.Login, Login = new CipherLoginModel() },
            CollectionIds = new List<Guid>()
        };

        if (shouldSucceed)
        {
            await sutProvider.Sut.PostAdmin(requestModel);
            await sutProvider.GetDependency<ICipherService>().ReceivedWithAnyArgs()
                .SaveAsync(default, default, default);
        }
        else
        {
            await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.PostAdmin(requestModel));
            await sutProvider.GetDependency<ICipherService>().DidNotReceiveWithAnyArgs()
                .SaveAsync(default, default, default);
        }
    }

    /// <summary>
    /// To be removed after FlexibleCollections is fully released
    /// </summary>
    [Theory]
    [BitAutoData(true, true)]
    [BitAutoData(false, true)]
    [BitAutoData(true, false)]
    [BitAutoData(false, false)]
    public async Task CanEditCiphersAsAdminAsync_NonFlexibleCollections(
        bool v1Enabled, bool shouldSucceed, CurrentContextOrganization organization, Guid userId, Cipher cipher, SutProvider<CiphersController> sutProvider
    )
    {
        cipher.OrganizationId = organization.Id;
        sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(shouldSucceed);

        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            FlexibleCollections = false,
            AllowAdminAccessToAllCollectionItems = false
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(v1Enabled);
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);

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
    [BitAutoData(false, false)]
    [BitAutoData(true, false)]
    public async Task CanEditCiphersAsAdminAsync_Providers(
        bool fcV1Enabled, bool shouldSucceed, Cipher cipher, CurrentContextOrganization organization, Guid userId, SutProvider<CiphersController> sutProvider
    )
    {
        cipher.OrganizationId = organization.Id;
        if (fcV1Enabled)
        {
            sutProvider.GetDependency<ICurrentContext>().ProviderUserForOrgAsync(organization.Id).Returns(shouldSucceed);
        }
        else
        {
            sutProvider.GetDependency<ICurrentContext>().EditAnyCollection(organization.Id).Returns(shouldSucceed);
        }
        sutProvider.GetDependency<ICurrentContext>().GetOrganization(organization.Id).Returns(organization);
        sutProvider.GetDependency<IUserService>().GetProperUserId(default).ReturnsForAnyArgs(userId);

        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipher.Id).Returns(cipher);
        sutProvider.GetDependency<ICipherRepository>().GetManyByOrganizationIdAsync(organization.Id).Returns(new List<Cipher> { cipher });

        sutProvider.GetDependency<IApplicationCacheService>().GetOrganizationAbilityAsync(organization.Id).Returns(new OrganizationAbility
        {
            Id = organization.Id,
            FlexibleCollections = fcV1Enabled, // Assume FlexibleCollections is enabled if v1 is enabled
            AllowAdminAccessToAllCollectionItems = false
        });
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.FlexibleCollectionsV1).Returns(fcV1Enabled);
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.RestrictProviderAccess).Returns(restrictProviders);

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

        if (fcV1Enabled)
        {
            await sutProvider.GetDependency<ICurrentContext>().Received().ProviderUserForOrgAsync(organization.Id);
        }
        else
        {
            await sutProvider.GetDependency<ICurrentContext>().Received().EditAnyCollection(organization.Id);
        }
    }
}
