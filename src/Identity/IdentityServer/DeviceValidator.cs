﻿using System.ComponentModel.DataAnnotations;
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

    /// <summary>
    /// Save a device to the database. If the device is already known, it will be returned.
    /// </summary>
    /// <param name="user">The user is assumed NOT null, still going to check though</param>
    /// <param name="request">Duende Validated Request that contains the data to create the device object</param>
    /// <returns>Returns null if user or device is malformed; The existing device if already in DB; a new device login</returns>
    public async Task<Device> SaveDeviceAsync(User user, ValidatedTokenRequest request)
    {
        var device = GetDeviceFromRequest(request);
        if (device != null && user != null)
        {
            var existingDevice = await GetKnownDeviceAsync(user, device);
            if (existingDevice == null)
            {
                device.UserId = user.Id;
                await _deviceService.SaveAsync(device);

                // This check just seems like a way to avoid sending an email on the first login
                // maybe vestigial?
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
        (await GetKnownDeviceAsync(user, GetDeviceFromRequest(request))) != default;

    protected async Task<Device> GetKnownDeviceAsync(User user, Device device)
    {
        if (user == null || device == null)
        {
            return default;
        }
        return await _deviceRepository.GetByIdentifierAsync(device.Identifier, user.Id);
    }

    private Device GetDeviceFromRequest(ValidatedRequest request)
    {
        var deviceIdentifier = request.Raw["DeviceIdentifier"]?.ToString();
        var requestDeviceType = request.Raw["DeviceType"]?.ToString();
        var deviceName = request.Raw["DeviceName"]?.ToString();
        var devicePushToken = request.Raw["DevicePushToken"]?.ToString();

        if (string.IsNullOrWhiteSpace(deviceIdentifier) ||
            string.IsNullOrWhiteSpace(requestDeviceType) ||
            string.IsNullOrWhiteSpace(deviceName) ||
            !Enum.TryParse(requestDeviceType, out DeviceType parsedDeviceType))
        {
            return null;
        }

        return new Device
        {
            Identifier = deviceIdentifier,
            Name = deviceName,
            Type = parsedDeviceType,
            PushToken = string.IsNullOrWhiteSpace(devicePushToken) ? null : devicePushToken
        };
    }
}
