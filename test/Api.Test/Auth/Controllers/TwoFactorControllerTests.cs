using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Request;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Api.Auth.Models.Response.TwoFactor;
using Bit.Core.AdminConsole.Entities;
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

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(TwoFactorController))]
[SutProviderCustomize]
public class TwoFactorControllerTests
{
    [Theory, BitAutoData]
    public async Task CheckAsync_UserNull_ThrowsUnauthorizedException(SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(null as User);

        // Act
        var result = () => sutProvider.Sut.GetDuo(request);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(result);
    }

    [Theory, BitAutoData]
    public async Task CheckAsync_BadSecret_ThrowsBadRequestException(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(false);

        // Act
        try
        {
            await sutProvider.Sut.GetDuo(request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("The model state is invalid.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task CheckAsync_CannotAccessPremium_ThrowsBadRequestException(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(false);

        // Act
        try
        {
            await sutProvider.Sut.GetDuo(request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("Premium status is required.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task GetDuo_Success(User user, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        user.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();
        SetupCheckAsyncToPass(sutProvider, user);

        // Act
        var result = await sutProvider.Sut.GetDuo(request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task PutDuo_InvalidConfiguration_ThrowsBadRequestException(User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupCheckAsyncToPass(sutProvider, user);
        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .Returns(false);

        // Act
        try
        {
            await sutProvider.Sut.PutDuo(request);
        }
        catch (BadRequestException e)
        {
            // Assert
            Assert.Equal("Duo configuration settings are not valid. Please re-check the Duo Admin panel.", e.Message);
        }
    }

    [Theory, BitAutoData]
    public async Task PutDuo_Success(User user, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        user.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();
        SetupCheckAsyncToPass(sutProvider, user);

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result = await sutProvider.Sut.PutDuo(request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
        Assert.Equal(user.TwoFactorProviders, request.ToUser(user).TwoFactorProviders);
    }

    [Theory, BitAutoData]
    public async Task CheckOrganizationAsync_ManagePolicies_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupCheckAsyncToPass(sutProvider, user);

        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(false);

        // Act
        var result = () => sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        await Assert.ThrowsAsync<NotFoundException>(result);
    }

    [Theory, BitAutoData]
    public async Task CheckOrganizationAsync_GetByIdAsync_ThrowsNotFoundException(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupCheckAsyncToPass(sutProvider, user);

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
    public async Task GetOrganizationDuo_Success(
        User user, Organization organization, SecretVerificationRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        organization.TwoFactorProviders = GetOrganizationTwoFactorDuoProvidersJson();
        SetupCheckAsyncToPass(sutProvider, user);
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);

        // Act
        var result = await sutProvider.Sut.GetOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task PutOrganizationDuo_InvalidConfiguration_ThrowsBadRequestException(
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupCheckAsyncToPass(sutProvider, user);
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);

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
        User user, Organization organization, UpdateTwoFactorDuoRequestModel request, SutProvider<TwoFactorController> sutProvider)
    {
        // Arrange
        SetupCheckAsyncToPass(sutProvider, user);
        SetupCheckOrganizationAsyncToPass(sutProvider, organization);
        organization.TwoFactorProviders = GetUserTwoFactorDuoProvidersJson();

        sutProvider.GetDependency<IDuoUniversalTokenService>()
            .ValidateDuoConfiguration(default, default, default)
            .ReturnsForAnyArgs(true);

        // Act
        var result =
            await sutProvider.Sut.PutOrganizationDuo(organization.Id.ToString(), request);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<TwoFactorDuoResponseModel>(result);
        Assert.Equal(organization.TwoFactorProviders, request.ToOrganization(organization).TwoFactorProviders);
    }


    private string GetUserTwoFactorDuoProvidersJson()
    {
        return
            "{\"2\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    private string GetOrganizationTwoFactorDuoProvidersJson()
    {
        return
            "{\"6\":{\"Enabled\":true,\"MetaData\":{\"ClientSecret\":\"secretClientSecret\",\"ClientId\":\"clientId\",\"Host\":\"example.com\"}}}";
    }

    /// <summary>
    /// Sets up the CheckAsync method to pass.
    /// </summary>
    /// <param name="sutProvider">uses bit auto data</param>
    /// <param name="user">uses bit auto data</param>
    private void SetupCheckAsyncToPass(SutProvider<TwoFactorController> sutProvider, User user)
    {
        sutProvider.GetDependency<IUserService>()
            .GetUserByPrincipalAsync(default)
            .ReturnsForAnyArgs(user);

        sutProvider.GetDependency<IUserService>()
            .VerifySecretAsync(default, default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IUserService>()
            .CanAccessPremium(default)
            .ReturnsForAnyArgs(true);
    }

    private void SetupCheckOrganizationAsyncToPass(SutProvider<TwoFactorController> sutProvider, Organization organization)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .ManagePolicies(default)
            .ReturnsForAnyArgs(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(default)
            .ReturnsForAnyArgs(organization);
    }
}
