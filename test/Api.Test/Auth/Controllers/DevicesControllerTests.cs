using Bit.Api.Controllers;
using Bit.Api.Models.Request;
using Bit.Api.Models.Response;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Auth.Controllers;

public class DevicesControllerTest
{
    private readonly IDeviceRepository _deviceRepositoryMock;
    private readonly IDeviceService _deviceServiceMock;
    private readonly IUserService _userServiceMock;
    private readonly IUntrustDevicesCommand _untrustDevicesCommand;
    private readonly IUserRepository _userRepositoryMock;
    private readonly ICurrentContext _currentContextMock;
    private readonly ILogger<DevicesController> _loggerMock;
    private readonly DevicesController _sut;

    public DevicesControllerTest()
    {
        _deviceRepositoryMock = Substitute.For<IDeviceRepository>();
        _deviceServiceMock = Substitute.For<IDeviceService>();
        _userServiceMock = Substitute.For<IUserService>();
        _untrustDevicesCommand = Substitute.For<IUntrustDevicesCommand>();
        _userRepositoryMock = Substitute.For<IUserRepository>();
        _currentContextMock = Substitute.For<ICurrentContext>();
        _loggerMock = Substitute.For<ILogger<DevicesController>>();

        _sut = new DevicesController(
            _deviceRepositoryMock,
            _deviceServiceMock,
            _userServiceMock,
            _untrustDevicesCommand,
            _userRepositoryMock,
            _currentContextMock,
            _loggerMock);
    }

    [Fact]
    public async Task Get_ReturnsExpectedResult()
    {
        // Arrange
        var userId = Guid.Parse("AD89E6F8-4E84-4CFE-A978-256CC0DBF974");

        var authDateTimeResponse = new DateTime(2024, 12, 9, 12, 0, 0);
        var devicesWithPendingAuthData = new List<DeviceAuthDetails>
        {
            new (
                new Device
                {
                    Id = Guid.Parse("B3136B10-7818-444F-B05B-4D7A9B8C48BF"),
                    UserId = userId,
                    Name = "chrome",
                    Type = DeviceType.ChromeBrowser,
                    Identifier = Guid.Parse("811E9254-F77C-48C8-AF0A-A181943F5708").ToString(),
                    EncryptedPublicKey = "PublicKey",
                    EncryptedUserKey = "UserKey",
                },
                Guid.Parse("E09D6943-D574-49E5-AC85-C3F12B4E019E"),
                authDateTimeResponse)
        };

        _userServiceMock.GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns(userId);
        _deviceRepositoryMock.GetManyByUserIdWithDeviceAuth(userId).Returns(devicesWithPendingAuthData);

        // Act
        var result = await _sut.GetAll();

        // Assert
        Assert.NotNull(result);
        Assert.IsType<ListResponseModel<DeviceAuthRequestResponseModel>>(result);
        var resultDevice = result.Data.First();
        Assert.Equal("chrome", resultDevice.Name);
        Assert.Equal(DeviceType.ChromeBrowser, resultDevice.Type);
        Assert.Equal(Guid.Parse("B3136B10-7818-444F-B05B-4D7A9B8C48BF"), resultDevice.Id);
        Assert.Equal(Guid.Parse("811E9254-F77C-48C8-AF0A-A181943F5708").ToString(), resultDevice.Identifier);
        Assert.Equal("PublicKey", resultDevice.EncryptedPublicKey);
        Assert.Equal("UserKey", resultDevice.EncryptedUserKey);
    }

    [Fact]
    public async Task Get_ThrowsException_WhenUserIdIsInvalid()
    {
        // Arrange
        _userServiceMock.GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns((Guid?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GetAll());
    }

    [Fact]
    public async Task PutSettings_UpdatesUseNewUi_ReturnsExpectedResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identifier = Guid.NewGuid().ToString();
        var device = new Device
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "chrome",
            Type = DeviceType.ChromeBrowser,
            Identifier = identifier,
            UseNewUi = false,
        };

        _userServiceMock.GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns(userId);
        _deviceRepositoryMock.GetByIdentifierAsync(identifier, userId).Returns(device);

        var model = new DeviceSettingsRequestModel { UseNewUi = true };

        // Act
        var result = await _sut.PutSettings(identifier, model);

        // Assert
        Assert.True(result.UseNewUi);
        await _deviceServiceMock.Received(1).SaveAsync(
            Arg.Is<Device>(d => d.Id == device.Id && d.UseNewUi));
    }

    [Fact]
    public async Task PutSettings_ThrowsNotFound_WhenDeviceDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var identifier = Guid.NewGuid().ToString();

        _userServiceMock.GetProperUserId(Arg.Any<System.Security.Claims.ClaimsPrincipal>()).Returns(userId);
        _deviceRepositoryMock.GetByIdentifierAsync(identifier, userId).Returns((Device)null);

        var model = new DeviceSettingsRequestModel { UseNewUi = true };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.PutSettings(identifier, model));
        await _deviceServiceMock.DidNotReceive().SaveAsync(Arg.Any<Device>());
    }
}
