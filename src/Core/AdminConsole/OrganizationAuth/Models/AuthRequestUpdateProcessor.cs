using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class AuthRequestUpdateProcessor
{
    public OrganizationAdminAuthRequest ProcessedAuthRequest { get; private set; }

    private OrganizationAdminAuthRequest _unprocessedAuthRequest { get; }
    private OrganizationAuthRequestUpdate _update { get; }
    private AuthRequestUpdateProcessorConfiguration _configuration { get; }

    public EventType OrganizationEventType => ProcessedAuthRequest?.Approved.Value ?? false
        ? EventType.OrganizationUser_ApprovedAuthRequest
        : EventType.OrganizationUser_RejectedAuthRequest;

    public AuthRequestUpdateProcessor(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate update,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        _unprocessedAuthRequest = authRequest;
        _update = update;
        _configuration = configuration;
    }

    public void Process()
    {
        if (_unprocessedAuthRequest == null)
        {
            throw new AuthRequestUpdateCouldNotBeProcessedException();
        }
        var isExpired = DateTime.UtcNow >
            _unprocessedAuthRequest.CreationDate
            .Add(_configuration.AuthRequestExpiresAfter);
        var isSpent = _unprocessedAuthRequest.Approved != null ||
            _unprocessedAuthRequest.ResponseDate.HasValue ||
            _unprocessedAuthRequest.AuthenticationDate.HasValue;
        var canBeProcessed = !isExpired &&
            !isSpent &&
            _unprocessedAuthRequest.Id == _update.Id &&
            _unprocessedAuthRequest.OrganizationId == _configuration.OrganizationId;
        if (!canBeProcessed)
        {
            throw new AuthRequestUpdateCouldNotBeProcessedException(_unprocessedAuthRequest.Id);
        }
        if (_update.Approved)
        {
            Approve();
            return;
        }
        Deny();
    }

    public async Task SendPushNotification(Func<OrganizationAdminAuthRequest, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved ?? false)
        {
            return;
        }
        await callback(ProcessedAuthRequest);
    }

    public async Task SendApprovalEmail(Func<OrganizationAdminAuthRequest, string, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved ?? false)
        {
            return;
        }
        var deviceTypeDisplayName = _unprocessedAuthRequest.RequestDeviceType.GetType()
            .GetMember(_unprocessedAuthRequest.RequestDeviceType.ToString())
            .FirstOrDefault()?
            // This unknown case can't be unit tested without adding an enum
            // with no display attribute. Faith and trust are required!
            .GetCustomAttribute<DisplayAttribute>()?.Name ?? "Unknown Device Type";
        var deviceTypeAndIdentifierDisplayString =
            string.IsNullOrWhiteSpace(_unprocessedAuthRequest.RequestDeviceIdentifier)
                ? deviceTypeDisplayName
                : $"{deviceTypeDisplayName} - {_unprocessedAuthRequest.RequestDeviceIdentifier}";
        await callback(ProcessedAuthRequest, deviceTypeAndIdentifierDisplayString);
    }

    private void Approve()
    {
        if (string.IsNullOrWhiteSpace(_update.Key))
        {
            throw new ApprovedAuthRequestIsMissingKeyException(_update.Id);
        }
        ProcessedAuthRequest = _unprocessedAuthRequest;
        ProcessedAuthRequest.Key = _update.Key;
        ProcessedAuthRequest.Approved = true;
        ProcessedAuthRequest.ResponseDate = DateTime.UtcNow;
    }

    private void Deny()
    {
        ProcessedAuthRequest = _unprocessedAuthRequest;
        ProcessedAuthRequest.Approved = false;
        ProcessedAuthRequest.ResponseDate = DateTime.UtcNow;
    }
}
