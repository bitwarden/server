using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
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
    private readonly DeviceValidator _sut;

    public string nullString;
    public DeviceValidatorTests()
    {
        _deviceService = Substitute.For<IDeviceService>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _globalSettings = new GlobalSettings();
        _mailService = Substitute.For<IMailService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _userService = Substitute.For<IUserService>();
        _sut = new DeviceValidator(
            _deviceService,
            _deviceRepository,
            _globalSettings,
            _mailService,
            _currentContext,
            _userService);
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_DeviceNull_ShouldReturnNull(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request.Raw["DeviceIdentifier"] = null;

        // Act
        var device = await _sut.SaveRequestingDeviceAsync(user, request);

        // Assert
        Assert.Null(device);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_UserIsNull_ShouldReturnNull(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        // Act
        var device = await _sut.SaveRequestingDeviceAsync(null, request);

        // Assert
        Assert.Null(device);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_NewDevice_ReturnsDevice_SendsEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        user.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(11);
        _globalSettings.DisableEmailNewDevice = false;

        // Act
        var device = await _sut.SaveRequestingDeviceAsync(user, request);

        // Assert
        Assert.NotNull(device);
        Assert.Equal(user.Id, device.UserId);
        Assert.Equal("DeviceIdentifier", device.Identifier);
        Assert.Equal(DeviceType.Android, device.Type);
        await _mailService.Received(1).SendNewDeviceLoggedInEmail(
            user.Email, "Android", Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_DisableNewDeviceEmail_ReturnsDevice_DoesNotSendEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        user.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(11);
        _globalSettings.DisableEmailNewDevice = true;

        // Act
        var device = await _sut.SaveRequestingDeviceAsync(user, request);

        // Assert
        Assert.NotNull(device);
        Assert.Equal(user.Id, device.UserId);
        Assert.Equal("DeviceIdentifier", device.Identifier);
        Assert.Equal(DeviceType.Android, device.Type);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            user.Email, "Android", Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_DeviceIsKnown_ShouldReturnDevice(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user,
        Device device)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        device.UserId = user.Id;
        device.Identifier = "DeviceIdentifier";
        device.Type = DeviceType.Android;
        device.Name = "DeviceName";
        device.PushToken = "DevicePushToken";
        _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id).Returns(device);

        // Act
        var resultDevice = await _sut.SaveRequestingDeviceAsync(user, request);

        // Assert
        Assert.Equal(device, resultDevice);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    [Obsolete("backwards compatiblity")]
    public async void SaveRequestingDeviceAsync_NewUser_DeviceUnknown_ShouldSaveDevice_NoEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);
        user.CreationDate = DateTime.UtcNow;
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>()).Returns(null as Device);

        // Act
        var device = await _sut.SaveRequestingDeviceAsync(user, request);

        // Assert
        Assert.NotNull(device);
        Assert.Equal(user.Id, device.UserId);
        Assert.Equal("DeviceIdentifier", device.Identifier);
        Assert.Equal(DeviceType.Android, device.Type);
        await _deviceService.Received(1).SaveAsync(device);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
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

    [Theory]
    [BitAutoData]
    public async void GetKnownDeviceAsync_DeviceNull_ReturnsFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        // Device raw data is null which will cause the device to be null

        // Act
        var result = await _sut.GetKnownDeviceAsync(user, null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
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

    [Theory]
    [BitAutoData]
    public async void GetKnownDeviceAsync_UserAndDeviceValid_ReturnsTrue(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user,
        Device device)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);
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
    public async void GetDeviceFromRequest_RawDeviceInfoNull_ReturnsNull(
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

    [Theory]
    [BitAutoData]
    public async void GetDeviceFromRequest_RawDeviceInfoValid_ReturnsDevice(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        // Act
        var result = DeviceValidator.GetDeviceFromRequest(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("DeviceIdentifier", result.Identifier);
        Assert.Equal("DeviceName", result.Name);
        Assert.Equal(DeviceType.Android, result.Type);
        Assert.Equal("DevicePushToken", result.PushToken);
    }

    [Theory]
    [BitAutoData]
    public async void SaveRequestingDeviceAsync_DeviceNull_ReturnsNull(User user)
    {
        // Arrange
        // AutoData arranges

        // Act
        var result = await _sut.SaveRequestingDeviceAsync(user, null as Device);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async void SaveRequestingDeviceAsync_UserNull_ReturnsNull(Device device)
    {
        // Arrange
        // AutoData arranges

        // Act
        var result = await _sut.SaveRequestingDeviceAsync(null, device);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public async void SaveRequestingDeviceAsync_ReturnsDevice(User user, Device device)
    {
        // Arrange
        device.UserId = Guid.Empty;
        _deviceService.SaveAsync(device).Returns(Task.FromResult(device));

        // Act
        var result = await _sut.SaveRequestingDeviceAsync(user, device);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId); // Method should set device 
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_DeviceNull_ReturnsFalseAndErrorMessage(
        User user,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        // AutoData arranges

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        var expectedErrorMessage = "invalid user or device";
        Assert.False(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_UserNull_ReturnsFalseAndErrorMessage(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        // AutoData arranges

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(null, request);

        // Assert
        var expectedErrorMessage = "invalid user or device";
        Assert.False(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpNull_ReturnsFalseAndErrorMessage(
        User user,
        Device device,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);
        _deviceRepository.GetManyByUserIdAsync(Arg.Any<Guid>()).Returns([device]);

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        var expectedErrorMessage = "new device verification required";
        Assert.False(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _userService.Received(1).SendOTPAsync(user);
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpEmpty_ReturnsFalseAndErrorMessage(
        User user,
        Device device,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);
        request.Raw["NewDeviceOtp"] = "";
        _deviceRepository.GetManyByUserIdAsync(Arg.Any<Guid>()).Returns([device]);

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        var expectedErrorMessage = "new device verification required";
        Assert.False(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _userService.Received(1).SendOTPAsync(user);
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpNotValid_ReturnsFalseAndErrorMessage(
        User user,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);
        request.Raw["NewDeviceOtp"] = "123456";
        _userService.VerifyOTPAsync(user, Arg.Any<string>()).Returns(false);

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        var expectedErrorMessage = "invalid otp";
        Assert.False(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _userService.Received(1).VerifyOTPAsync(user, Arg.Any<string>());
        await _deviceService.Received(0).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpValid_ReturnsTrueAndNull(
        User user,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);
        request.Raw["NewDeviceOtp"] = "123456";
        _userService.VerifyOTPAsync(user, Arg.Any<string>()).Returns(true);

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        string? expectedErrorMessage = null;
        Assert.True(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _userService.Received(1).VerifyOTPAsync(user, Arg.Any<string>());
        await _deviceService.Received(1).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void HandleNewDeviceVerificationAsync_NewDeviceOtpNull_NoDevicesInDB_ReturnsTrue(
        User user,
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        AddValidDeviceToRequest(request);

        // Act
        var result = await _sut.HandleNewDeviceVerificationAsync(user, request);

        // Assert
        string? expectedErrorMessage = null;
        Assert.True(result.Item1);
        Assert.Equal(result.Item2, expectedErrorMessage);
        await _userService.Received(0).VerifyOTPAsync(user, Arg.Any<string>());
        await _deviceService.Received(1).SaveAsync(Arg.Any<Device>());
    }

    [Theory]
    [BitAutoData]
    public async void NewDeviceOtpRequest_NewDeviceOtpNull_ReturnsFalse([AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        // Autodata arranges

        // Act
        var result = DeviceValidator.NewDeviceOtpRequest(request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void NewDeviceOtpRequest_NewDeviceOtpNull_ReturnsTrue([AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request.Raw["NewDeviceOtp"] = "123456";

        // Act
        var result = DeviceValidator.NewDeviceOtpRequest(request);

        // Assert
        Assert.True(result);
    }

    private ValidatedTokenRequest AddValidDeviceToRequest(ValidatedTokenRequest request)
    {
        request.Raw["DeviceIdentifier"] = "DeviceIdentifier";
        request.Raw["DeviceType"] = "Android"; // must be valid device type
        request.Raw["DeviceName"] = "DeviceName";
        request.Raw["DevicePushToken"] = "DevicePushToken";
        return request;
    }
}
