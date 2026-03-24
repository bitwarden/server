using Bit.Core.Auth.UserFeatures.Devices;
using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
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
    public async Task BumpByIdAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        Guid deviceId,
        string identifier)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(identifier)
            .Returns(true);

        await sutProvider.Sut.BumpByIdAsync(deviceId, identifier);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpLastActivityDateByIdAsync(Arg.Any<Guid>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpByIdAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        Guid deviceId,
        string identifier)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(identifier)
            .Returns(false);

        await sutProvider.Sut.BumpByIdAsync(deviceId, identifier);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpLastActivityDateByIdAsync(deviceId);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordBumpAsync(identifier);
    }

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        string identifier,
        Guid userId)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(identifier)
            .Returns(true);

        await sutProvider.Sut.BumpByIdentifierAsync(identifier, userId);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpLastActivityDateByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceLastActivityDateCommand> sutProvider,
        string identifier,
        Guid userId)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .HasBeenBumpedTodayAsync(identifier)
            .Returns(false);

        await sutProvider.Sut.BumpByIdentifierAsync(identifier, userId);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpLastActivityDateByIdentifierAsync(identifier, userId);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordBumpAsync(identifier);
    }
}
