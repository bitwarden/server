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
public class BumpDeviceDataCommandTests
{
    // --- BumpAsync (by Device) ---

    [Theory, BitAutoData]
    public async Task BumpAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceDataCommand> sutProvider,
        Device device,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceDataCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, clientVersion)
            .Returns(true);

        await sutProvider.Sut.BumpAsync(device, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpDeviceDataByIdAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await sutProvider.GetDependency<IDeviceDataCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceDataCommand> sutProvider,
        Device device,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceDataCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, clientVersion)
            .Returns(false);

        await sutProvider.Sut.BumpAsync(device, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpDeviceDataByIdAsync(device.Id, clientVersion);
        await sutProvider.GetDependency<IDeviceDataCacheService>()
            .Received(1)
            .RecordBumpAsync(device.UserId, device.Identifier, clientVersion);
    }

    [Theory, BitAutoData]
    public async Task BumpAsync_GivenCacheMiss_VersionNull_PersistsNullVersion(
        SutProvider<BumpDeviceDataCommand> sutProvider,
        Device device)
    {
        sutProvider.GetDependency<IDeviceDataCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, null)
            .Returns(false);

        await sutProvider.Sut.BumpAsync(device, null);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpDeviceDataByIdAsync(device.Id, null);
        await sutProvider.GetDependency<IDeviceDataCacheService>()
            .Received(1)
            .RecordBumpAsync(device.UserId, device.Identifier, null);
    }

    // --- BumpByIdentifierAndUserIdAsync ---

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAndUserIdAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<BumpDeviceDataCommand> sutProvider,
        string identifier,
        Guid userId,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceDataCacheService>()
            .IsUpToDateAsync(userId, identifier, clientVersion)
            .Returns(true);

        await sutProvider.Sut.BumpByIdentifierAndUserIdAsync(identifier, userId, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .BumpDeviceDataByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
        await sutProvider.GetDependency<IDeviceDataCacheService>()
            .DidNotReceive()
            .RecordBumpAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task BumpByIdentifierAndUserIdAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<BumpDeviceDataCommand> sutProvider,
        string identifier,
        Guid userId,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceDataCacheService>()
            .IsUpToDateAsync(userId, identifier, clientVersion)
            .Returns(false);

        await sutProvider.Sut.BumpByIdentifierAndUserIdAsync(identifier, userId, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .BumpDeviceDataByIdentifierAndUserIdAsync(identifier, userId, clientVersion);
        await sutProvider.GetDependency<IDeviceDataCacheService>()
            .Received(1)
            .RecordBumpAsync(userId, identifier, clientVersion);
    }
}
