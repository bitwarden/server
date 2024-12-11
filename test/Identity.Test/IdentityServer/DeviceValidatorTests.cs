using Bit.Core;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.Identity.IdentityServer.RequestValidators;
using Bit.Test.Common.AutoFixture.Attributes;
using Duende.IdentityServer.Validation;
using NSubstitute;
using Xunit;
using AuthFixtures = Bit.Identity.Test.AutoFixture;

namespace Bit.Identity.Test.IdentityServer;

public class DeviceValidatorTests
{
    private readonly IDeviceService _deviceService;
    private readonly IDeviceRepository _deviceRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IMailService _mailService;
    private readonly ICurrentContext _currentContext;
    private readonly IUserService _userService;
    private readonly IFeatureService _featureService;
    private readonly DeviceValidator _sut;

    public DeviceValidatorTests()
    {
        _deviceService = Substitute.For<IDeviceService>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _globalSettings = new GlobalSettings();
        _mailService = Substitute.For<IMailService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _userService = Substitute.For<IUserService>();
        _featureService = Substitute.For<IFeatureService>();
        _sut = new DeviceValidator(
            _deviceService,
            _deviceRepository,
            _globalSettings,
            _mailService,
            _currentContext,
            _userService,
            _featureService);
    }

    [Theory, BitAutoData]
    public async void GetKnownDeviceAsync_UserNull_ReturnsFalse(
        Device device)
    {
        // Arrange
        // AutoData arrages

        // Act
        var result = await _sut.GetKnownDeviceAsync(null, device);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async void GetKnownDeviceAsync_DeviceNull_ReturnsFalse(
        User user)
    {
        // Arrange
        // Device raw data is null which will cause the device to be null

        // Act
        var result = await _sut.GetKnownDeviceAsync(user, null);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async void GetKnownDeviceAsync_DeviceNotInDatabase_ReturnsFalse(
        User user,
        Device device)
    {
        // Arrange
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
                         .Returns(null as Device);
        // Act
        var result = await _sut.GetKnownDeviceAsync(user, device);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public async void GetKnownDeviceAsync_UserAndDeviceValid_ReturnsTrue(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user,
        Device device)
    {
        // Arrange
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
                         .Returns(device);
        // Act
        var result = await _sut.GetKnownDeviceAsync(user, device);

        // Assert
        Assert.NotNull(result);
    }

    [Theory]
    [BitAutoData("not null", "Android", "")]
    [BitAutoData("not null", "", "not null")]
    [BitAutoData("", "Android", "not null")]
    public void GetDeviceFromRequest_RawDeviceInfoNull_ReturnsNull(
        string deviceIdentifier,
        string deviceType,
        string deviceName,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request.Raw["DeviceIdentifier"] = deviceIdentifier;
        request.Raw["DeviceType"] = deviceType;
        request.Raw["DeviceName"] = deviceName;

        // Act
        var result = DeviceValidator.GetDeviceFromRequest(request);

        // Assert
        Assert.Null(result);
    }

    [Theory, BitAutoData]
    public void GetDeviceFromRequest_RawDeviceInfoValid_ReturnsDevice(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);

        // Act
        var result = DeviceValidator.GetDeviceFromRequest(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DeviceIdentifier", result.Identifier);
        Assert.Equal("DeviceName", result.Name);
        Assert.Equal(DeviceType.Android, result.Type);
        Assert.Equal("DevicePushToken", result.PushToken);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_DeviceNull_ContextModified_ReturnsFalse(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        context.Device = null;

        // Act
        Assert.NotNull(context.User);
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.False(result);
        Assert.NotNull(context.CustomResponse["ErrorModel"]);
        var expectedErrorModel = new ErrorResponseModel("no device information provided");
        var actualResponse = (ErrorResponseModel)context.CustomResponse["ErrorModel"];
        Assert.Equal(expectedErrorModel.Message, actualResponse.Message);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_RequestDeviceKnown_ContextDeviceModified_ReturnsTrue(
        Device device,
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        context.Device = null;
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns(device);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.True(result);
        Assert.False(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.NotNull(context.Device);
        Assert.Equal(context.Device, device);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_ContextDeviceKnown_ContextDeviceModified_ReturnsTrue(
    Device databaseDevice,
    CustomValidatorRequestContext context,
    [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns(databaseDevice);
        // we want to show that the context device is updated when the device is known
        Assert.NotEqual(context.Device, databaseDevice);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.True(result);
        Assert.False(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.Equal(context.Device, databaseDevice);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_NewDeviceVerificationFeatureFlagFalse_SendsEmail_ReturnsTrue(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        AddValidDeviceToRequest(request);
        _globalSettings.DisableEmailNewDevice = false;
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(false);
        // set user creation to more than 10 minutes ago
        context.User.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(11);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        await _mailService.Received(1).SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_NewDeviceVerificationFeatureFlagFalse_NewUser_DoesNotSendEmail_ReturnsTrue(
    CustomValidatorRequestContext context,
    [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        AddValidDeviceToRequest(request);
        _globalSettings.DisableEmailNewDevice = false;
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(false);
        // set user creation to less than 10 minutes ago
        context.User.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(9);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        await _mailService.Received(0).SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_NewDeviceVerificationFeatureFlagFalse_DisableEmailTrue_DoesNotSendEmail_ReturnsTrue(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        AddValidDeviceToRequest(request);
        _globalSettings.DisableEmailNewDevice = true;
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(false);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        await _mailService.Received(0).SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
        Assert.True(result);
    }

    [Theory]
    [BitAutoData("webauthn")]
    [BitAutoData("refresh_token")]
    [BitAutoData("authorization_code")]
    [BitAutoData("client_credentials")]
    public async void ValidateRequestDeviceAsync_GrantTypeNotPassword_SavesDevice_ReturnsTrue(
        string grantType,
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);

        request.GrantType = grantType;

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_IsAuthRequest_SavesDevice_ReturnsTrue(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);

        request.Raw.Add("AuthRequest", "authRequest");

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_TwoFactorRequired_SavesDevice_ReturnsTrue(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);

        context.TwoFactorRequired = true;

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void ValidateRequestDeviceAsync_SsoRequired_SavesDevice_ReturnsTrue(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        context.KnownDevice = false;
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(context.Device.Identifier, context.User.Id)
            .Returns(null as Device);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification)
            .Returns(true);

        context.SsoRequired = true;

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(1).SaveAsync(context.Device);
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async void HandleNewDeviceVerificationAsync_UserNull_ContextModified_ReturnsInvalidUser(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification).Returns(true);
        _globalSettings.EnableNewDeviceVerification = true;

        context.User = null;

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.False(result);
        Assert.NotNull(context.CustomResponse["ErrorModel"]);
        var expectedErrorMessage = "no device information provided";
        var actualResponse = (ErrorResponseModel)context.CustomResponse["ErrorModel"];
        Assert.Equal(expectedErrorMessage, actualResponse.Message);
    }

    [Theory, BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpValid_ReturnsSuccess(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification).Returns(true);
        _globalSettings.EnableNewDeviceVerification = true;

        var newDeviceOtp = "123456";
        request.Raw.Add("NewDeviceOtp", newDeviceOtp);

        _userService.VerifyOTPAsync(context.User, newDeviceOtp).Returns(true);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _userService.Received(0).SendOTPAsync(context.User);
        await _deviceService.Received(1).SaveAsync(context.Device);

        Assert.True(result);
        Assert.False(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.Equal(context.User.Id, context.Device.UserId);
        Assert.NotNull(context.Device);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("123456")]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpInvalid_ReturnsInvalidNewDeviceOtp(
        string newDeviceOtp,
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification).Returns(true);
        _globalSettings.EnableNewDeviceVerification = true;

        request.Raw.Add("NewDeviceOtp", newDeviceOtp);

        _userService.VerifyOTPAsync(context.User, newDeviceOtp).Returns(false);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _userService.DidNotReceive().SendOTPAsync(Arg.Any<User>());
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.False(result);
        Assert.NotNull(context.CustomResponse["ErrorModel"]);
        var expectedErrorMessage = "invalid new device otp";
        var actualResponse = (ErrorResponseModel)context.CustomResponse["ErrorModel"];
        Assert.Equal(expectedErrorMessage, actualResponse.Message);
    }

    [Theory, BitAutoData]
    public async void HandleNewDeviceVerificationAsync_UserHasNoDevices_ReturnsSuccess(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification).Returns(true);
        _globalSettings.EnableNewDeviceVerification = true;
        _deviceRepository.GetManyByUserIdAsync(context.User.Id).Returns([]);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _userService.Received(0).VerifyOTPAsync(Arg.Any<User>(), Arg.Any<string>());
        await _userService.Received(0).SendOTPAsync(Arg.Any<User>());
        await _deviceService.Received(1).SaveAsync(context.Device);

        Assert.True(result);
        Assert.False(context.CustomResponse.ContainsKey("ErrorModel"));
        Assert.Equal(context.User.Id, context.Device.UserId);
        Assert.NotNull(context.Device);
    }

    [Theory, BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpEmpty_UserHasDevices_ReturnsNewDeviceVerificationRequired(
        CustomValidatorRequestContext context,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        ArrangeForHandleNewDeviceVerificationTest(context, request);
        _featureService.IsEnabled(FeatureFlagKeys.NewDeviceVerification).Returns(true);
        _globalSettings.EnableNewDeviceVerification = true;
        _deviceRepository.GetManyByUserIdAsync(context.User.Id).Returns([new Device()]);

        // Act
        var result = await _sut.ValidateRequestDeviceAsync(request, context);

        // Assert
        await _userService.Received(1).SendOTPAsync(context.User);
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());

        Assert.False(result);
        Assert.NotNull(context.CustomResponse["ErrorModel"]);
        var expectedErrorMessage = "new device verification required";
        var actualResponse = (ErrorResponseModel)context.CustomResponse["ErrorModel"];
        Assert.Equal(expectedErrorMessage, actualResponse.Message);
    }

    [Theory, BitAutoData]
    public void NewDeviceOtpRequest_NewDeviceOtpNull_ReturnsFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        // Autodata arranges

        // Act
        var result = DeviceValidator.NewDeviceOtpRequest(request);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void NewDeviceOtpRequest_NewDeviceOtpNotNull_ReturnsTrue(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request.Raw["NewDeviceOtp"] = "123456";

        // Act
        var result = DeviceValidator.NewDeviceOtpRequest(request);

        // Assert
        Assert.True(result);
    }

    private static void AddValidDeviceToRequest(ValidatedTokenRequest request)
    {
        request.Raw["DeviceIdentifier"] = "DeviceIdentifier";
        request.Raw["DeviceType"] = "Android"; // must be valid device type
        request.Raw["DeviceName"] = "DeviceName";
        request.Raw["DevicePushToken"] = "DevicePushToken";
    }

    /// <summary>
    /// Configures the request context to facilitate testing the HandleNewDeviceVerificationAsync method.
    /// </summary>
    /// <param name="context">test context</param>
    /// <param name="request">test request</param>
    private static void ArrangeForHandleNewDeviceVerificationTest(
        CustomValidatorRequestContext context,
        ValidatedTokenRequest request)
    {
        context.KnownDevice = false;
        request.GrantType = "password";
        context.TwoFactorRequired = false;
        context.SsoRequired = false;
    }
}
