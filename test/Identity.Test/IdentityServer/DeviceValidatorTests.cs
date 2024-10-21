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
    private readonly DeviceValidator _sut;

    public DeviceValidatorTests()
    {
        _deviceService = Substitute.For<IDeviceService>();
        _deviceRepository = Substitute.For<IDeviceRepository>();
        _globalSettings = new GlobalSettings();
        _mailService = Substitute.For<IMailService>();
        _currentContext = Substitute.For<ICurrentContext>();
        _sut = new DeviceValidator(
            _deviceService,
            _deviceRepository,
            _globalSettings,
            _mailService,
            _currentContext);
    }

    [Theory]
    [BitAutoData]
    public async void SaveDeviceAsync_DeviceNull_ShouldReturnNull(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request.Raw["DeviceIdentifier"] = null;

        // Act
        var device = await _sut.SaveDeviceAsync(user, request);

        // Assert
        Assert.Null(device);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async void SaveDeviceAsync_UserIsNull_ShouldReturnNull(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        // Act
        var device = await _sut.SaveDeviceAsync(null, request);

        // Assert
        Assert.Null(device);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async void SaveDeviceAsync_ExistingUser_NewDevice_ReturnsDevice_SendsEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        user.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(11);
        _globalSettings.DisableEmailNewDevice = false;

        // Act
        var device = await _sut.SaveDeviceAsync(user, request);

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
    public async void SaveDeviceAsync_ExistingUser_NewDevice_ReturnsDevice_SendEmailFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        user.CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(11);
        _globalSettings.DisableEmailNewDevice = true;

        // Act
        var device = await _sut.SaveDeviceAsync(user, request);

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
    public async void SaveDeviceAsync_DeviceIsKnown_ShouldReturnDevice(
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
        var resultDevice = await _sut.SaveDeviceAsync(user, request);

        // Assert
        Assert.Equal(device, resultDevice);
        await _mailService.DidNotReceive().SendNewDeviceLoggedInEmail(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<string>());
    }

    [Theory]
    [BitAutoData]
    public async void SaveDeviceAsync_NewUser_DeviceUnknown_ShouldSaveDevice_NoEmail(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);
        user.CreationDate = DateTime.UtcNow;
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>()).Returns(null as Device);

        // Act
        var device = await _sut.SaveDeviceAsync(user, request);

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
    public async void KnownDeviceAsync_UserNull_ReturnsFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);

        // Act
        var result = await _sut.KnownDeviceAsync(null, request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void KnownDeviceAsync_DeviceNull_ReturnsFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        // Device raw data is null which will cause the device to be null

        // Act
        var result = await _sut.KnownDeviceAsync(user, request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void KnownDeviceAsync_DeviceNotInDatabase_ReturnsFalse(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
                         .Returns(null as Device);
        // Act
        var result = await _sut.KnownDeviceAsync(user, request);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async void KnownDeviceAsync_UserAndDeviceValid_ReturnsTrue(
        [AuthFixtures.ValidatedTokenRequest] ValidatedTokenRequest request,
        User user,
        Device device)
    {
        // Arrange
        request = AddValidDeviceToRequest(request);
        _deviceRepository.GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>())
                         .Returns(device);
        // Act
        var result = await _sut.KnownDeviceAsync(user, request);

        // Assert
        Assert.True(result);
    }

    private ValidatedTokenRequest AddValidDeviceToRequest(ValidatedTokenRequest request)
    {
        request.Raw["DeviceIdentifier"] = "DeviceIdentifier";
        request.Raw["DeviceType"] = "Android";
        request.Raw["DeviceName"] = "DeviceName";
        request.Raw["DevicePushToken"] = "DevicePushToken";
        return request;
    }
}
