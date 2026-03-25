using Bit.Core.Auth.UserFeatures.Devices;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Auth.UserFeatures.Devices;

[SutProviderCustomize]
public class BumpDeviceLastActivityDateCommandTests
{
    [Theory, BitAutoData]
    public async Task BumpAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        Device device)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(device.UserId, device.Identifier)
            .Returns(true);

        await sutProvider.Sut.BumpAsync(device);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpLastActivityDateByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        Device device)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(device.UserId, device.Identifier)
            .Returns(false);

        await sutProvider.Sut.BumpAsync(device);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpLastActivityDateByIdAsync(device.Id);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordBumpAsync(device.UserId, device.Identifier);
    }

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        string identifier,
        Guid userId)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(userId, identifier)
            .Returns(true);

        await sutProvider.Sut.BumpByIdentifierAsync(identifier, userId);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpLastActivityDateByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        string identifier,
        Guid userId)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(userId, identifier)
            .Returns(false);

        await sutProvider.Sut.BumpByIdentifierAsync(identifier, userId);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpLastActivityDateByIdentifierAsync(identifier, userId);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordBumpAsync(userId, identifier);
    }
}
