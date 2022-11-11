using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

public class DeviceServiceTests
{
    [Fact]
    public async Task DeviceSaveShouldUpdateRevisionDate()
    {
        var deviceRepo = Substitute.For<IDeviceRepository>();
        var deviceService = new DeviceService(deviceRepo);

        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var device = new Device
        {
            Id = id,
            Name = "test device",
            Type = DeviceType.Android,
            UserId = userId,
            PushToken = "testtoken",
            Identifier = "testid"
        };
        await deviceService.SaveAsync(device);

        Assert.True(device.RevisionDate - DateTime.UtcNow < TimeSpan.FromSeconds(1));
    }
}
