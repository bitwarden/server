using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Exceptions;
using Bit.Core.Auth.Models.Api.Request.AuthRequest;
using Bit.Core.Auth.Services.Implementations;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
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

    /// <summary>
    /// Story: AdminApproval AuthRequests should have a longer expiration time by default and non-AdminApproval ones
    /// should expire after 15 minutes by default.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AdminApproval, "-10.00:00:00")]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock, "-00:16:00")]
    [BitAutoData(AuthRequestType.Unlock, "-00:16:00")]
    public async Task GetValidatedAuthRequestAsync_IfExpired_ReturnsNull(
        AuthRequestType authRequestType,
        TimeSpan creationTimeBeforeNow,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.Type = authRequestType;
        authRequest.CreationDate = DateTime.UtcNow.Add(creationTimeBeforeNow);
        authRequest.Approved = false;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var foundAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode);

        Assert.Null(foundAuthRequest);
    }

    /// <summary>
    /// Story: Once a AdminApproval type has been approved it has a different expiration time based on time
    /// after the response.
    /// </summary>
    [Theory]
    [BitAutoData]
    public async Task GetValidatedAuthRequestAsync_AdminApprovalApproved_HasLongerExpiration_ReturnsRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.Type = AuthRequestType.AdminApproval;
        authRequest.Approved = true;
        authRequest.ResponseDate = DateTime.UtcNow.Add(TimeSpan.FromHours(-13));

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        var validatedAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode);

        Assert.Null(validatedAuthRequest);
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

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth
            .Returns(new Settings.GlobalSettings.PasswordlessAuthSettings());

        var foundAuthRequest = await sutProvider.Sut.GetValidatedAuthRequestAsync(authRequest.Id, authRequest.AccessCode);

        Assert.NotNull(foundAuthRequest);
    }

    [Theory, BitAutoData]
    public async Task CreateAuthRequestAsync_NoUser_ThrowsBadRequest(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel)
    {
        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns(DeviceType.Android);

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns((User?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAuthRequestAsync(createModel));
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

    /// <summary>
    /// Story: Non-AdminApproval requests should be created without a known device if the settings is set to <c>false</c>
    /// Non-AdminApproval ones should also have a push notification sent about them.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock)]
    [BitAutoData(AuthRequestType.Unlock)]
    [BitAutoData(new object?[1] { null })]
    public async Task CreateAuthRequestAsync_CreatesAuthRequest(
        AuthRequestType? authRequestType,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel,
        User user)
    {
        user.Email = createModel.Email;
        createModel.Type = authRequestType;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns(DeviceType.Android);

        sutProvider.GetDependency<ICurrentContext>()
            .IpAddress
            .Returns("1.1.1.1");

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth.KnownDevicesOnly
            .Returns(false);

        sutProvider.GetDependency<IAuthRequestRepository>()
            .CreateAsync(Arg.Any<AuthRequest>())
            .Returns(c => c.ArgAt<AuthRequest>(0));

        var createdAuthRequest = await sutProvider.Sut.CreateAuthRequestAsync(createModel);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received()
            .PushAuthRequestAsync(createdAuthRequest);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .CreateAsync(createdAuthRequest);
    }

    /// <summary>
    /// Story: Since an AllowAnonymous endpoint calls this method we need
    /// to verify that a device was able to be found via ICurrentContext
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock)]
    [BitAutoData(AuthRequestType.Unlock)]
    public async Task CreateAuthRequestAsync_NoDeviceType_ThrowsBadRequest(
        AuthRequestType authRequestType,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel,
        User user)
    {
        user.Email = createModel.Email;
        createModel.Type = authRequestType;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(createModel.Email)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns((DeviceType?)null);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.CreateAuthRequestAsync(createModel));
    }

    /// <summary>
    /// Story: If a user happens to exist to more than one organization, we will send the device approval request to
    /// each of them.
    /// </summary>
    [Theory, BitAutoData]
    public async Task CreateAuthRequestAsync_AdminApproval_CreatesForEachOrganization(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequestCreateRequestModel createModel,
        User user,
        OrganizationUser organizationUser1,
        OrganizationUser organizationUser2)
    {
        createModel.Type = AuthRequestType.AdminApproval;
        user.Email = createModel.Email;
        organizationUser1.UserId = user.Id;
        organizationUser2.UserId = user.Id;

        sutProvider.GetDependency<IUserRepository>()
            .GetByEmailAsync(user.Email)
            .Returns(user);

        sutProvider.GetDependency<ICurrentContext>()
            .DeviceType
            .Returns(DeviceType.ChromeExtension);

        sutProvider.GetDependency<ICurrentContext>()
            .UserId
            .Returns(user.Id);

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth.KnownDevicesOnly
            .Returns(false);


        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyByUserAsync(user.Id)
            .Returns(new List<OrganizationUser>
            {
                organizationUser1,
                organizationUser2,
            });

        sutProvider.GetDependency<IAuthRequestRepository>()
            .CreateAsync(Arg.Any<AuthRequest>())
            .Returns(c => c.ArgAt<AuthRequest>(0));

        var authRequest = await sutProvider.Sut.CreateAuthRequestAsync(createModel);

        Assert.Equal(organizationUser1.OrganizationId, authRequest.OrganizationId);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<AuthRequest>(o => o.OrganizationId == organizationUser1.OrganizationId));

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received(1)
            .CreateAsync(Arg.Is<AuthRequest>(o => o.OrganizationId == organizationUser2.OrganizationId));

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received(2)
            .CreateAsync(Arg.Any<AuthRequest>());

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogUserEventAsync(user.Id, EventType.User_RequestedDeviceApproval);
    }

    /// <summary>
    /// Story: When an <see cref="AuthRequest"> is approved we want to update it in the database so it cannot have
    /// it's status changed again and we want to push a notification to let the user know of the approval.
    /// In the case of the AdminApproval we also want to log an event.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AdminApproval, "7b055ea1-38be-42d0-b2e4-becb2340f8df")]
    [BitAutoData(AuthRequestType.Unlock, null)]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock, null)]
    public async Task UpdateAuthRequestAsync_ValidResponse_SendsResponse(
        AuthRequestType authRequestType,
        Guid? organizationId,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Approved = null;
        authRequest.OrganizationId = organizationId;
        authRequest.Type = authRequestType;

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

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new OrganizationUser
            {
                UserId = authRequest.UserId,
                OrganizationId = organizationId.GetValueOrDefault(),
            });

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth
            .Returns(new Settings.GlobalSettings.PasswordlessAuthSettings());

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        var udpatedAuthRequest = await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel);

        Assert.Equal("my_hash", udpatedAuthRequest.MasterPasswordHash);

        // On approval, the response date should be set to current date
        Assert.NotNull(udpatedAuthRequest.ResponseDate);
        AssertHelper.AssertRecent(udpatedAuthRequest.ResponseDate!.Value);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received(1)
            .ReplaceAsync(udpatedAuthRequest);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAuthRequestResponseAsync(udpatedAuthRequest);

        var expectedNumberOfCalls = organizationId.HasValue ? 1 : 0;
        await sutProvider.GetDependency<IEventService>()
            .Received(expectedNumberOfCalls)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(ou => ou.UserId == authRequest.UserId && ou.OrganizationId == organizationId),
                EventType.OrganizationUser_ApprovedAuthRequest);
    }

    /// <summary>
    /// Story: When an <see cref="AuthRequest"> is rejected we want to update it in the database so it cannot have
    /// it's status changed again but we do not want to send a push notification to the original device
    /// so as to not leak that it was rejected. In the case of an AdminApproval type we do want to log an event though
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AdminApproval, "7b055ea1-38be-42d0-b2e4-becb2340f8df")]
    [BitAutoData(AuthRequestType.Unlock, null)]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock, null)]
    public async Task UpdateAuthRequestAsync_ResponseNotApproved_DoesNotLeakRejection(
        AuthRequestType authRequestType,
        Guid? organizationId,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        // Give it a recent creation time which is valid for all types of AuthRequests
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Type = authRequestType;
        // Has not been decided already
        authRequest.Approved = null;
        authRequest.OrganizationId = organizationId;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        // Setup a device for all requests even though it will not be called for verification in a AdminApproval
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Identifier = "test_identifier",
        };

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth
            .Returns(new Settings.GlobalSettings.PasswordlessAuthSettings());

        sutProvider.GetDependency<IDeviceRepository>()
            .GetByIdentifierAsync(device.Identifier, authRequest.UserId)
            .Returns(device);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(new OrganizationUser
            {
                UserId = authRequest.UserId,
                OrganizationId = organizationId.GetValueOrDefault(),
            });

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = false,
            MasterPasswordHash = "my_hash",
        };

        var udpatedAuthRequest = await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel);

        Assert.Equal(udpatedAuthRequest.MasterPasswordHash, authRequest.MasterPasswordHash);
        Assert.False(udpatedAuthRequest.Approved);
        Assert.NotNull(udpatedAuthRequest.ResponseDate);
        AssertHelper.AssertRecent(udpatedAuthRequest.ResponseDate!.Value);

        await sutProvider.GetDependency<IAuthRequestRepository>()
            .Received()
            .ReplaceAsync(udpatedAuthRequest);

        await sutProvider.GetDependency<IPushNotificationService>()
            .DidNotReceiveWithAnyArgs()
            .PushAuthRequestResponseAsync(udpatedAuthRequest);

        var expectedNumberOfCalls = organizationId.HasValue ? 1 : 0;

        await sutProvider.GetDependency<IEventService>()
            .Received(expectedNumberOfCalls)
            .LogOrganizationUserEventAsync(
                Arg.Is<OrganizationUser>(ou => ou.UserId == authRequest.UserId && ou.OrganizationId == organizationId),
                EventType.OrganizationUser_RejectedAuthRequest);
    }

    /// <summary>
    /// Story: A bad actor is able to get ahold of the request id of a valid <see cref="AuthRequest" />
    /// and tries to approve it from their own Bitwarden account. We need to validate that the currently signed in user
    /// is the same user that originally created the request and we want to pretend it does not exist at all by throwing
    /// NotFoundException.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock)]
    [BitAutoData(AuthRequestType.Unlock)]
    public async Task UpdateAuthRequestAsync_InvalidUser_ThrowsNotFound(
        AuthRequestType authRequestType,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        Guid authenticatedUserId)
    {
        // Give it a recent creation date so that it is valid
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        // The request hasn't been Approved/Disapproved already
        authRequest.Approved = null;
        // Has an type that needs the UserId property validated
        authRequest.Type = authRequestType;

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
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authenticatedUserId, updateModel));
    }

    /// <summary>
    /// Story: A user created this auth request and does not approve/reject the request
    /// for 16 minutes, which is past the default expiration time. This auth request
    /// will be purged from the database soon but might exist for some amount of time after it's expiration
    /// this method should throw a NotFoundException since it theoretically should not exist, this
    /// could be a user finally clicking Approve after the request sitting on their phone for a while.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock, "-00:16:00")]
    [BitAutoData(AuthRequestType.Unlock, "-00:16:00")]
    [BitAutoData(AuthRequestType.AdminApproval, "-8.00:00:00")]
    public async Task UpdateAuthRequestAsync_OldAuthRequest_ThrowsNotFound(
        AuthRequestType authRequestType,
        TimeSpan timeBeforeCreation,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        // AuthRequest's have a default valid lifetime of only 15 minutes, make it older than that
        authRequest.CreationDate = DateTime.UtcNow.Add(timeBeforeCreation);
        // Make it so that the user has not made a decision on this request
        authRequest.Approved = null;
        // Make it one of the types that doesn't have longer expiration i.e AdminApproval
        authRequest.Type = authRequestType;

        // The item should still exist in the database
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        // Represents the user finally clicking approve.
        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "test_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        await Assert.ThrowsAsync<NotFoundException>(
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel));
    }

    /// <summary>
    /// Story: non-AdminApproval types need to validate that the device used to respond to the
    /// request is a known device to the authenticated user.
    /// </summary>
    [Theory]
    [BitAutoData(AuthRequestType.AuthenticateAndUnlock)]
    [BitAutoData(AuthRequestType.Unlock)]
    public async Task UpdateAuthRequestAsync_InvalidDeviceIdentifier_ThrowsBadRequest(
        AuthRequestType authRequestType,
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Approved = null;
        authRequest.Type = authRequestType;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        sutProvider.GetDependency<IDeviceRepository>()
            .GetByIdentifierAsync("invalid_identifier", authRequest.UserId)
            .Returns((Device?)null);

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth
            .Returns(new Settings.GlobalSettings.PasswordlessAuthSettings());

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            DeviceIdentifier = "invalid_identifier",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        await Assert.ThrowsAsync<BadRequestException>(
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel));
    }

    /// <summary>
    /// Story: Once the destiny of an AuthRequest has been decided, it should be considered immutable
    /// and new update request should be blocked.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_AlreadyApprovedOrRejected_ThrowsDuplicateAuthRequestException(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest)
    {
        authRequest.Approved = true;

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

        await Assert.ThrowsAsync<DuplicateAuthRequestException>(
            async () => await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel));
    }

    /// <summary>
    /// Story: An admin approves a request for one of their org users. For auditing purposes we need to
    /// log an event that correlates the action for who the request was approved for. On approval we also need to
    /// push the notification to the user.
    /// </summary>
    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_AdminApproved_LogsEvent(
        SutProvider<AuthRequestService> sutProvider,
        AuthRequest authRequest,
        OrganizationUser organizationUser)
    {
        authRequest.CreationDate = DateTime.UtcNow.AddMinutes(-10);
        authRequest.Type = AuthRequestType.AdminApproval;
        authRequest.OrganizationId = organizationUser.OrganizationId;
        authRequest.Approved = null;

        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequest.Id)
            .Returns(authRequest);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(authRequest.OrganizationId!.Value, authRequest.UserId)
            .Returns(organizationUser);

        sutProvider.GetDependency<IGlobalSettings>()
            .PasswordlessAuth
            .Returns(new Settings.GlobalSettings.PasswordlessAuthSettings());

        var updateModel = new AuthRequestUpdateRequestModel
        {
            Key = "test_key",
            RequestApproved = true,
            MasterPasswordHash = "my_hash",
        };

        var updatedAuthRequest = await sutProvider.Sut.UpdateAuthRequestAsync(authRequest.Id, authRequest.UserId, updateModel);

        Assert.Equal("my_hash", updatedAuthRequest.MasterPasswordHash);
        Assert.Equal("test_key", updatedAuthRequest.Key);
        Assert.True(updatedAuthRequest.Approved);
        Assert.NotNull(updatedAuthRequest.ResponseDate);
        AssertHelper.AssertRecent(updatedAuthRequest.ResponseDate!.Value);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(
                Arg.Is(organizationUser), Arg.Is(EventType.OrganizationUser_ApprovedAuthRequest));

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushAuthRequestResponseAsync(authRequest);
    }

    [Theory, BitAutoData]
    public async Task UpdateAuthRequestAsync_BadId_ThrowsNotFound(
        SutProvider<AuthRequestService> sutProvider,
        Guid authRequestId)
    {
        sutProvider.GetDependency<IAuthRequestRepository>()
            .GetByIdAsync(authRequestId)
            .Returns((AuthRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(async () => await sutProvider.Sut.UpdateAuthRequestAsync(
            authRequestId, Guid.NewGuid(), new AuthRequestUpdateRequestModel()));
    }
}
