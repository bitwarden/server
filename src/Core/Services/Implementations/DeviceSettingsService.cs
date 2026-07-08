using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Services;

public class DeviceSettingsService : IDeviceSettingsService
{
    private readonly ICurrentContext _currentContext;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IFeatureService _featureService;

    // The service is registered scoped (per request). Cache the resolved device so that multiple
    // reads within a request cost a single DB round trip, matching the cheap-to-call-anywhere feel
    // of IFeatureService.
    private Device? _device;
    private bool _loaded;

    public DeviceSettingsService(
        ICurrentContext currentContext,
        IDeviceRepository deviceRepository,
        IFeatureService featureService)
    {
        _currentContext = currentContext;
        _deviceRepository = deviceRepository;
        _featureService = featureService;
    }

    public async Task<bool> UseNewUiAsync()
    {
        // The new UI is gated behind a beta feature flag. When it is off, the new UI is
        // unavailable to everyone regardless of the per-device setting; the stored value is left
        // untouched so a user's choice is restored if the flag is turned back on.
        if (!_featureService.IsEnabled(FeatureFlagKeys.NewUiBetaSwitch))
        {
            return false;
        }

        return (await GetCurrentDeviceAsync())?.UseNewUi ?? false;
    }

    private async Task<Device?> GetCurrentDeviceAsync()
    {
        if (_loaded)
        {
            return _device;
        }
        _loaded = true;

        if (_currentContext.UserId is not { } userId ||
            string.IsNullOrWhiteSpace(_currentContext.DeviceIdentifier))
        {
            return _device = null;
        }

        _device = await _deviceRepository.GetByIdentifierAsync(_currentContext.DeviceIdentifier, userId);
        return _device;
    }
}
