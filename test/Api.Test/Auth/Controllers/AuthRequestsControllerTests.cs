using System.Security.Claims;
using Bit.Api.Auth.Controllers;
using Bit.Api.Auth.Models.Response;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.Services;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

[ControllerCustomize(typeof(AuthRequestsController))]
[SutProviderCustomize]
public class AuthRequestsControllerTests
{
    const string _testGlobalSettingsBaseUri = "https://vault.test.dev";

    [Theory, BitAutoData]
    public async Task Get_ReturnsExpectedResult(
        SutProvider<AuthRequestsController> sutProvider,
        User user,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetManyByUserIdAsync(user.Id)
            .Returns([authRequest]);

        // Act
        var result = await sutProvider.Sut.Get();

        // Assert
        Assert.NotNull(result);
        var expectedCount = 1;
        Assert.Equal(result.Data.Count(), expectedCount);
        Assert.IsType<ListResponseModel<AuthRequestResponseModel>>(result);
    }

    [Theory, BitAutoData]
    public async Task GetById_ThrowsNotFoundException(
        SutProvider<AuthRequestsController> sutProvider,
        User user,
        AuthRequest authRequest)
    {
        // Arrange
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IAuthRequestService>()
            .GetAuthRequestAsync(authRequest.Id, user.Id)
            .Returns((AuthRequest)null);

        // Act
        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
           () => sutProvider.Sut.Get(authRequest.Id));
    }

    [Theory, BitAutoData]
    public async Task GetById_ReturnsAuthRequest(
        SutProvider<AuthRequestsController> sutProvider,
        User user,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IAuthRequestService>()
            .GetAuthRequestAsync(authRequest.Id, user.Id)
            .Returns(authRequest);

        // Act
        var result = await sutProvider.Sut.Get(authRequest.Id);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AuthRequestResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task GetPending_ReturnsExpectedResult(
        SutProvider<AuthRequestsController> sutProvider,
        User user,
        PendingAuthRequestDetails authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetManyPendingAuthRequestByUserId(user.Id)
            .Returns([authRequest]);

        // Act
        var result = await sutProvider.Sut.GetPendingAuthRequestsAsync();

        // Assert
        Assert.NotNull(result);
        var expectedCount = 1;
        Assert.Equal(result.Data.Count(), expectedCount);
        Assert.IsType<ListResponseModel<PendingAuthRequestResponseModel>>(result);
    }

    [Theory, BitAutoData]
    public async Task GetResponseById_ThrowsNotFoundException(
        SutProvider<AuthRequestsController> sutProvider,
        AuthRequest authRequest)
    {
        // Arrange
        sutProvider.GetDependency<IAuthRequestService>()
            .GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode)
            .Returns((AuthRequest)null);

        // Act
        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(
           () => sutProvider.Sut.GetResponse(authRequest.Id, authRequest.AccessCode));
    }

    [Theory, BitAutoData]
    public async Task GetResponseById_ReturnsAuthRequest(
        SutProvider<AuthRequestsController> sutProvider,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        sutProvider.GetDependency<IAuthRequestService>()
            .GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode)
            .Returns(authRequest);

        // Act
        var result = await sutProvider.Sut.GetResponse(authRequest.Id, authRequest.AccessCode);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AuthRequestResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task Post_AdminApprovalRequest_ThrowsBadRequestException(
        SutProvider<AuthRequestsController> sutProvider,
        AuthRequestCreateRequestModel authRequest)
    {
        // Arrange
        authRequest.Type = AuthRequestType.AdminApproval;

        // Act
        // Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
           () => sutProvider.Sut.Post(authRequest));

        var expectedMessage = "You must be authenticated to create a request of that type.";
        Assert.Equal(exception.Message, expectedMessage);
    }

    [Theory, BitAutoData]
    public async Task Post_ReturnsAuthRequest(
        SutProvider<AuthRequestsController> sutProvider,
        AuthRequestCreateRequestModel requestModel,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        requestModel.Type = AuthRequestType.AuthenticateAndUnlock;
        sutProvider.GetDependency<IAuthRequestService>()
            .CreateAuthRequestAsync(requestModel)
            .Returns(authRequest);

        // Act
        var result = await sutProvider.Sut.Post(requestModel);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AuthRequestResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task PostAdminRequest_ReturnsAuthRequest(
        SutProvider<AuthRequestsController> sutProvider,
        AuthRequestCreateRequestModel requestModel,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        requestModel.Type = AuthRequestType.AuthenticateAndUnlock;
        sutProvider.GetDependency<IAuthRequestService>()
            .CreateAuthRequestAsync(requestModel)
            .Returns(authRequest);

        // Act
        var result = await sutProvider.Sut.PostAdminRequest(requestModel);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AuthRequestResponseModel>(result);
    }

    [Theory, BitAutoData]
    public async Task Put_ReturnsAuthRequest(
        SutProvider<AuthRequestsController> sutProvider,
        User user,
        AuthRequestUpdateRequestModel requestModel,
        AuthRequest authRequest)
    {
        // Arrange
        SetBaseServiceUri(sutProvider);

        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(user.Id);

        sutProvider.GetDependency<IAuthRequestService>()
            .UpdateAuthRequestAsync(authRequest.Id, user.Id, requestModel)
            .Returns(authRequest);

        // Act
        var result = await sutProvider.Sut
                .Put(authRequest.Id, requestModel);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<AuthRequestResponseModel>(result);
    }

    private void SetBaseServiceUri(SutProvider<AuthRequestsController> sutProvider)
    {
        sutProvider.GetDependency<IGlobalSettings>()
            .BaseServiceUri
            .Vault
            .Returns(_testGlobalSettingsBaseUri);
    }
}
