using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.IdentityServer;

public interface IDeviceValidator
{
    Task<Device> SaveDeviceAsync(User user, ValidatedTokenRequest request);
    Task<bool> KnownDeviceAsync(User user, ValidatedTokenRequest request);
}

public class DeviceValidator(
    IDeviceService deviceService,
    IDeviceRepository deviceRepository,
    GlobalSettings globalSettings,
    IMailService mailService,
    ICurrentContext currentContext) : IDeviceValidator
{
    private readonly IDeviceService _deviceService = deviceService;
    private readonly IDeviceRepository _deviceRepository = deviceRepository;
    private readonly GlobalSettings _globalSettings = globalSettings;
    private readonly IMailService _mailService = mailService;
    private readonly ICurrentContext _currentContext = currentContext;

    public async Task<Device> SaveDeviceAsync(User user, ValidatedTokenRequest request)
    {
        var device = GetDeviceFromRequest(request);
        if (device != null)
        {
            var existingDevice = await GetKnownDeviceAsync(user, request);
            if (existingDevice == null)
            {
                device.UserId = user.Id;
                await _deviceService.SaveAsync(device);

                var now = DateTime.UtcNow;
                if (now - user.CreationDate > TimeSpan.FromMinutes(10))
                {
                    var deviceType = device.Type.GetType().GetMember(device.Type.ToString())
                        .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
                    if (!_globalSettings.DisableEmailNewDevice)
                    {
                        await _mailService.SendNewDeviceLoggedInEmail(user.Email, deviceType, now,
                            _currentContext.IpAddress);
                    }
                }
                return device;
            }
            return existingDevice;
        }
        return null;
    }

    public async Task<bool> KnownDeviceAsync(User user, ValidatedTokenRequest request) =>
        (await GetKnownDeviceAsync(user, request)) != default;

    protected async Task<Device> GetKnownDeviceAsync(User user, ValidatedTokenRequest request)
    {
        if (user == null)
        {
            return default;
        }

        return await _deviceRepository.GetByIdentifierAsync(GetDeviceFromRequest(request).Identifier, user.Id);
    }

    private Device GetDeviceFromRequest(ValidatedRequest request)
    {
        var deviceIdentifier = request.Raw["DeviceIdentifier"]?.ToString();
        var deviceType = request.Raw["DeviceType"]?.ToString();
        var deviceName = request.Raw["DeviceName"]?.ToString();
        var devicePushToken = request.Raw["DevicePushToken"]?.ToString();

        if (string.IsNullOrWhiteSpace(deviceIdentifier) || string.IsNullOrWhiteSpace(deviceType) ||
            string.IsNullOrWhiteSpace(deviceName) || !Enum.TryParse(deviceType, out DeviceType type))
        {
            return null;
        }

        return new Device
        {
            Identifier = deviceIdentifier,
            Name = deviceName,
            Type = type,
            PushToken = string.IsNullOrWhiteSpace(devicePushToken) ? null : devicePushToken
        };
    }
}
