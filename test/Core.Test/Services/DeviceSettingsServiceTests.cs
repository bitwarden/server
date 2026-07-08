using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class DeviceSettingsServiceTests
{
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public async Task UseNewUiAsync_FlagEnabled_ReturnsDeviceValue(
        bool useNewUi,
        SutProvider<DeviceSettingsService> sutProvider,
        Guid userId,
        string identifier,
        Device device)
    {
        device.UseNewUi = useNewUi;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(identifier);
        sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(identifier, userId).Returns(device);

        var result = await sutProvider.Sut.UseNewUiAsync();

        Assert.Equal(useNewUi, result);
    }

    [Theory]
    [BitAutoData]
    public async Task UseNewUiAsync_FlagDisabled_ReturnsFalse_WithoutQueryingDevice(
        SutProvider<DeviceSettingsService> sutProvider,
        Guid userId,
        string identifier,
        Device device)
    {
        // Even with the device opted in, the flag being off forces false and skips the DB read.
        device.UseNewUi = true;
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(false);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(identifier);
        sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(identifier, userId).Returns(device);

        var result = await sutProvider.Sut.UseNewUiAsync();

        Assert.False(result);
        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive().GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UseNewUiAsync_ReturnsFalse_WhenNoUser(
        SutProvider<DeviceSettingsService> sutProvider,
        string identifier)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns((Guid?)null);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(identifier);

        var result = await sutProvider.Sut.UseNewUiAsync();

        Assert.False(result);
        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive().GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UseNewUiAsync_ReturnsFalse_WhenNoDeviceIdentifier(
        SutProvider<DeviceSettingsService> sutProvider,
        Guid userId)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns((string)null);

        var result = await sutProvider.Sut.UseNewUiAsync();

        Assert.False(result);
        await sutProvider.GetDependency<IDeviceRepository>()
            .DidNotReceive().GetByIdentifierAsync(Arg.Any<string>(), Arg.Any<Guid>());
    }

    [Theory]
    [BitAutoData]
    public async Task UseNewUiAsync_ReturnsFalse_WhenDeviceNotFound(
        SutProvider<DeviceSettingsService> sutProvider,
        Guid userId,
        string identifier)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(identifier);
        sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(identifier, userId).Returns((Device)null);

        var result = await sutProvider.Sut.UseNewUiAsync();

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task UseNewUiAsync_CachesDevice_OnlyQueriesRepositoryOnce(
        SutProvider<DeviceSettingsService> sutProvider,
        Guid userId,
        string identifier,
        Device device)
    {
        sutProvider.GetDependency<IFeatureService>().IsEnabled(FeatureFlagKeys.NewUiBetaSwitch).Returns(true);
        sutProvider.GetDependency<ICurrentContext>().UserId.Returns(userId);
        sutProvider.GetDependency<ICurrentContext>().DeviceIdentifier.Returns(identifier);
        sutProvider.GetDependency<IDeviceRepository>().GetByIdentifierAsync(identifier, userId).Returns(device);

        await sutProvider.Sut.UseNewUiAsync();
        await sutProvider.Sut.UseNewUiAsync();

        await sutProvider.GetDependency<IDeviceRepository>()
            .Received(1).GetByIdentifierAsync(identifier, userId);
    }
}
