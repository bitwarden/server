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
public class UpdateDeviceLastActivityCommandTests
{
    // --- UpdateAsync (by Device) ---

    [Theory, BitAutoData]
    public async Task UpdateAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<UpdateDeviceLastActivityCommand> sutProvider,
        Device device,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, clientVersion)
            .Returns(true);

        await sutProvider.Sut.UpdateAsync(device, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .UpdateLastActivityByIdAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordUpdateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<UpdateDeviceLastActivityCommand> sutProvider,
        Device device,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, clientVersion)
            .Returns(false);

        await sutProvider.Sut.UpdateAsync(device, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpdateLastActivityByIdAsync(device.Id, clientVersion);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordUpdateAsync(device.UserId, device.Identifier, clientVersion);
    }

    [Theory, BitAutoData]
    public async Task UpdateAsync_GivenCacheMiss_VersionNull_PersistsNullVersion(
        SutProvider<UpdateDeviceLastActivityCommand> sutProvider,
        Device device)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .IsUpToDateAsync(device.UserId, device.Identifier, null)
            .Returns(false);

        await sutProvider.Sut.UpdateAsync(device, null);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpdateLastActivityByIdAsync(device.Id, null);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordUpdateAsync(device.UserId, device.Identifier, null);
    }

    // --- UpdateByIdentifierAndUserIdAsync ---

    [Theory, BitAutoData]
    public async Task UpdateByIdentifierAndUserIdAsync_GivenCacheHit_DoesNotCallRepository(
        SutProvider<UpdateDeviceLastActivityCommand> sutProvider,
        string identifier,
        Guid userId,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .IsUpToDateAsync(userId, identifier, clientVersion)
            .Returns(true);

        await sutProvider.Sut.UpdateByIdentifierAndUserIdAsync(identifier, userId, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive()
            .UpdateLastActivityByIdentifierAndUserIdAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>());
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .DidNotReceive()
            .RecordUpdateAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Theory, BitAutoData]
    public async Task UpdateByIdentifierAndUserIdAsync_GivenCacheMiss_CallsRepositoryAndRecordsCache(
        SutProvider<UpdateDeviceLastActivityCommand> sutProvider,
        string identifier,
        Guid userId,
        string clientVersion)
    {
        sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .IsUpToDateAsync(userId, identifier, clientVersion)
            .Returns(false);

        await sutProvider.Sut.UpdateByIdentifierAndUserIdAsync(identifier, userId, clientVersion);

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1)
            .UpdateLastActivityByIdentifierAndUserIdAsync(identifier, userId, clientVersion);
        await sutProvider.GetDependency<IDeviceLastActivityCacheService>()
            .Received(1)
            .RecordUpdateAsync(userId, identifier, clientVersion);
    }
}
