using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Identity.TokenProviders;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using static Bit.Api.Test.Auth.Controllers.TwoFactor.TwoFactorControllerTestHelpers;

namespace Bit.Api.Test.Auth.Controllers.TwoFactor;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerOrganizationDuoTests
{
    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_Success(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);
        SetupOrganizationAccessToPass(sutProvider, organization);

        // Act
        var result = await sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorOrganizationDuoResponseModel>(result);
        Assert.NotNull(result.Duo);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task GetOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupValidateUserBySecretToPass(sutProvider, user);

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_InvalidConfiguration_ThrowsBadRequestException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(false);

        // Act
        try
        {
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("Duo configuration settings are not valid. Please re-check the Duo Admin panel.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_Success(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result =
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorOrganizationDuoUpdateResponseModel>(result);
        Assert.NotNull(result.Duo);
        Assert.Equal(organization.TwoFactorProviders, request.ToOrganization(organization).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorDuoUpdateRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(null as Organization);

        // Act
        var result = () => sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task DeleteOrganizationDuo_Success(
        User user, Organization organization, TwoFactorOrganizationDuoDeleteRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupGetUserByPrincipalAsync(sutProvider, user);
        SetupUserVerificationTokenFactoryToUnprotectInto(
            sutProvider, ValidUserVerificationTokenableFor(user, TwoFactorProviderType.OrganizationDuo));
        SetupOrganizationAccessToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();

        // Act
        await sutProvider.Sut.DeleteOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await sutProvider.GetDependency<IOrganizationService>()
            .Received(1)
            .DisableTwoFactorProviderAsync(organization, TwoFactorProviderType.OrganizationDuo);
    }
}
