// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Diagnostics;
using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Models.Api;
using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Platform.PushRegistration;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.Platform.Push;

/// <summary>
/// Routes for push relay: functionality that facilitates communication
/// between self hosted organizations and Bitwarden cloud.
/// </summary>
[Route("push")]
[Authorize("Push")]
[SelfHosted(NotSelfHostedOnly = true)]
public class PushController : Controller
{
    private readonly IPushRegistrationService _pushRegistrationService;
    private readonly IPushRelayer _pushRelayer;
    private readonly IWebHostEnvironment _environment;
    private readonly ICurrentContext _currentContext;
    private readonly IGlobalSettings _globalSettings;

    public PushController(
        IPushRegistrationService pushRegistrationService,
        IPushRelayer pushRelayer,
        IWebHostEnvironment environment,
        ICurrentContext currentContext,
        IGlobalSettings globalSettings)
    {
        _currentContext = currentContext;
        _environment = environment;
        _pushRegistrationService = pushRegistrationService;
        _pushRelayer = pushRelayer;
        _globalSettings = globalSettings;
    }

    [HttpPost("register")]
    public async Task RegisterAsync([FromBody] PushRegistrationRequestModel model)
    {
        CheckUsage();
        await _pushRegistrationService.CreateOrUpdateRegistrationAsync(new PushRegistrationData(model.PushToken),
            Prefix(model.DeviceId), Prefix(model.UserId), Prefix(model.Identifier), model.Type,
            model.OrganizationIds?.Select(Prefix) ?? [], model.InstallationId);
    }

    [HttpPost("delete")]
    public async Task DeleteAsync([FromBody] PushDeviceRequestModel model)
    {
        CheckUsage();
        await _pushRegistrationService.DeleteRegistrationAsync(Prefix(model.Id));
    }

    [HttpPut("add-organization")]
    public async Task AddOrganizationAsync([FromBody] PushUpdateRequestModel model)
    {
        CheckUsage();
        await _pushRegistrationService.AddUserRegistrationOrganizationAsync(
            model.Devices.Select(d => Prefix(d.Id)),
            Prefix(model.OrganizationId));
    }

    [HttpPut("delete-organization")]
    public async Task DeleteOrganizationAsync([FromBody] PushUpdateRequestModel model)
    {
        CheckUsage();
        await _pushRegistrationService.DeleteUserRegistrationOrganizationAsync(
            model.Devices.Select(d => Prefix(d.Id)),
            Prefix(model.OrganizationId));
    }

    [HttpPost("send")]
    public async Task SendAsync([FromBody] PushSendRequestModel<JsonElement> model)
    {
        CheckUsage();

        NotificationTarget target;
        Guid targetId;

        if (model.InstallationId.HasValue)
        {
            if (_currentContext.InstallationId!.Value != model.InstallationId.Value)
            {
                throw new BadRequestException("InstallationId does not match current context.");
            }

            target = NotificationTarget.Installation;
            targetId = _currentContext.InstallationId.Value;
        }
        else if (model.UserId.HasValue)
        {
            target = NotificationTarget.User;
            targetId = model.UserId.Value;
        }
        else if (model.OrganizationId.HasValue)
        {
            target = NotificationTarget.Organization;
            targetId = model.OrganizationId.Value;
        }
        else
        {
            throw new UnreachableException("Model validation should have prevented getting here.");
        }

        var notification = new RelayedNotification
        {
            Type = model.Type,
            Target = target,
            TargetId = targetId,
            Payload = model.Payload,
            Identifier = model.Identifier,
            DeviceId = model.DeviceId,
            ClientType = model.ClientType,
        };

        await _pushRelayer.RelayAsync(_currentContext.InstallationId.Value, notification);
    }

    private string Prefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return $"{_currentContext.InstallationId!.Value}_{value}";
    }

    private void CheckUsage()
    {
        if (CanUse())
        {
            return;
        }

        throw new BadRequestException("Not correctly configured for push relays.");
    }

    private bool CanUse()
    {
        if (_environment.IsDevelopment())
        {
            return true;
        }

        return _currentContext.InstallationId.HasValue && !_globalSettings.SelfHosted;
    }
}
