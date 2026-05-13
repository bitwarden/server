using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Auth.UserFeatures.DeviceTrust;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bit.Core.Auth.UserFeatures.Devices;

public static class DeviceServiceCollectionExtensions
{
    public static void AddDeviceServices(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDeviceDataCacheService, DeviceDataCacheService>();
        services.TryAddScoped<IUntrustDevicesCommand, UntrustDevicesCommand>();
        services.TryAddScoped<IBumpDeviceDataCommand, BumpDeviceDataCommand>();
    }
}
