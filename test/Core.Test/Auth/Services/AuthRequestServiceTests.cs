using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services.Implementations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

#nullable enable

namespace Bit.Core.Test.Auth.Services;

[SutProviderCustomize]
public class AuthRequestServiceTests
{
    [Theory, BitAutoData]
    public async Task GetAuthRequestAsync_IfDifferentUser_ReturnsNull(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        Guid authRequestId,
        Guid userId)
    {
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequestId)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetAuthRequestAsync(authRequestId, userId);

        Assert.Null(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task GetAuthRequestAsync_IfSameUser_ReturnsAuthRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        Guid authRequestId)
    {
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequestId)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetAuthRequestAsync(authRequestId, authRequest.UserId);

        Assert.NotNull(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task GetValidatedAuthRequestAsync_IfCodeNotValid_ReturnsNull(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        string accessCode)
    {
        authRequest.CreationDate = DateTime.UtcNow;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, accessCode);

        Assert.Null(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task GetValidatedAuthRequestAsync_IfExpired_ReturnsNull(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddHours(-1);

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode);

        Assert.Null(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task GetValidatedAuthRequestAsync_IfValid_ReturnsAuthRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-2);

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode);

        Assert.NotNull(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task CreateAuthRequestAsync_NoUser_ThrowsNotFound(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel)
    {
        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns((User?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CreateAuthRequestAsync(createModel));
    }

    [Theory, BitAutoData]
    public async Task CreateAuthRequestAsync_NoKnownDevice_ThrowsBadRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel,
        User user)
    {
        user.Email = createModel.Email;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns(DeviceType.Android);

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth.KnownDevicesOnly
            .Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAuthRequestAsync(createModel));
    }

    [Theory, BitAutoData]
    public async Task CreateAuthRequestAsync_CreatesAuthRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel,
        User user)
    {
        user.Email = createModel.Email;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns(DeviceType.Android);

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth.KnownDevicesOnly
            .Returns(false);

        await sutProvider.Sut.CreateAuthRequestAsync(createModel);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received()
            .PushAuthRequestAsync(Arg.Any<AuthRequest>());

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .CreateAsync(Arg.Any<AuthRequest>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_ValidResponse_SendsResponse(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Approved = null;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            Identifier = "test_identifier",
        };

        sutProvider.GetDependency<IDeviceRepository>()
            .GetByIdentifierAsync(device.Identifier, authRequest.UserId)
            .Returns(device);

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        var udpatedAuthRequest = await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel);

        Assert.Equal("my_hash", udpatedAuthRequest.MasterPasswordHash);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .ReplaceAsync(udpatedAuthRequest);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received()
            .PushAuthRequestResponseAsync(udpatedAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_ResponseNotApproved_DoesNotLeakRejection(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Approved = null;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            Identifier = "test_identifier",
        };

        sutProvider.GetDependency<IDeviceRepository>()
            .GetByIdentifierAsync(device.Identifier, authRequest.UserId)
            .Returns(device);

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = false,
            MasterPasswordHash = "my_hash",
        };

        var udpatedAuthRequest = await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel);

        Assert.Equal(udpatedAuthRequest.MasterPasswordHash, authRequest.MasterPasswordHash);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .ReplaceAsync(udpatedAuthRequest);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAuthRequestResponseAsync(udpatedAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_InvalidUser_ThrowsNotFound(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        Guid userId)
    {
        // Give it a recent creation date so that it is valid
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Approved = false;

        // Auth request should not be null
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        // Give it a randomly generated userId such that it won't be valid for the AuthRequest
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, userId, updateModel));
    }

    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_OldAuthRequest_ThrowsNotFound(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        // AuthRequest's have a valid lifetime of only 15 minutes, make it older than that
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-16);
        authRequest.Approved = false;

        // Auth request should not be null
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        // Give it a randomly generated userId such that it won't be valid for the AuthRequest
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel));
    }
}
