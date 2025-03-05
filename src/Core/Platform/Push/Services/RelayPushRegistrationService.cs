﻿using Bit.Core.Enums;
using Bit.Core.IdentityServer;
using Bit.Core.Models.Api;
using Bit.Core.NotificationHub;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

public class RelayPushRegistrationService : BaseIdentityClientService, IPushRegistrationService
{
    public RelayPushRegistrationService(
        IHttpClientFactory httpFactory,
        GlobalSettings globalSettings,
        ILogger<RelayPushRegistrationService> logger)
        : base(
            httpFactory,
            globalSettings.PushRelayBaseUri,
            globalSettings.Installation.IdentityUri,
            ApiScopes.ApiPush,
            $"installation.{globalSettings.Installation.Id}",
            globalSettings.Installation.Key,
            logger)
    {
    }

    public async Task CreateOrUpdateRegistrationAsync(PushRegistrationData pushData, string deviceId, string userId,
        string identifier, DeviceType type, IEnumerable<string> organizationIds, Guid installationId)
    {
        var requestModel = new PushRegistrationRequestModel
        {
            DeviceId = deviceId,
            Identifier = identifier,
            PushToken = pushData.Token,
            Type = type,
            UserId = userId,
            OrganizationIds = organizationIds,
            InstallationId = installationId
        };
        await SendAsync(HttpMethod.Post, "push/register", requestModel);
    }

    public async Task DeleteRegistrationAsync(string deviceId)
    {
        var requestModel = new PushDeviceRequestModel
        {
            Id = deviceId,
        };
        await SendAsync(HttpMethod.Post, "push/delete", requestModel);
    }

    public async Task AddUserRegistrationOrganizationAsync(
        IEnumerable<string> deviceIds, string organizationId)
    {
        if (!deviceIds.Any())
        {
            return;
        }

        var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
        await SendAsync(HttpMethod.Put, "push/add-organization", requestModel);
    }

    public async Task DeleteUserRegistrationOrganizationAsync(
        IEnumerable<string> deviceIds, string organizationId)
    {
        if (!deviceIds.Any())
        {
            return;
        }

        var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
        await SendAsync(HttpMethod.Put, "push/delete-organization", requestModel);
    }
}
