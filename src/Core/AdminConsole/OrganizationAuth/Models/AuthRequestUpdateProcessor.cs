using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class AuthRequestUpdateProcessor
{
    public OrganizationAdminAuthRequest ProcessedAuthRequest { get; private set; }

    private OrganizationAdminAuthRequest _unprocessedAuthRequest { get; }
    private OrganizationAuthRequestUpdate _updates { get; }
    private AuthRequestUpdateProcessorConfiguration _configuration { get; }

    public EventType OrganizationEventType => ProcessedAuthRequest?.Approved.Value ?? false
        ? EventType.OrganizationUser_ApprovedAuthRequest
        : EventType.OrganizationUser_RejectedAuthRequest;

    public AuthRequestUpdateProcessor(
        OrganizationAdminAuthRequest authRequest,
        OrganizationAuthRequestUpdate updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        _unprocessedAuthRequest = authRequest;
        _updates = updates;
        _configuration = configuration;
    }

    public AuthRequestUpdateProcessor Process()
    {
        var isExpired = DateTime.UtcNow >
            _unprocessedAuthRequest.CreationDate
            .Add(_configuration.AuthRequestExpiresAfter);
        var isSpent = _unprocessedAuthRequest == null ||
            _unprocessedAuthRequest.Approved != null ||
            _unprocessedAuthRequest.ResponseDate.HasValue ||
            _unprocessedAuthRequest.AuthenticationDate.HasValue;
        var canBeProcessed = !isExpired &&
            !isSpent &&
            _unprocessedAuthRequest.Id == _updates.Id &&
            _unprocessedAuthRequest.OrganizationId == _configuration.OrganizationId;
        if (!canBeProcessed)
        {
            throw new AuthRequestUpdateCouldNotBeProcessedException(_unprocessedAuthRequest.Id);
        }
        return _updates.Approved
            ? Approve()
            : Deny();
    }

    public async Task<AuthRequestUpdateProcessor> SendPushNotification(Func<OrganizationAdminAuthRequest, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved ?? false || callback == null)
        {
            return this;
        }
        await callback(ProcessedAuthRequest);
        return this;
    }

    public async Task<AuthRequestUpdateProcessor> SendNewDeviceEmail(Func<OrganizationAdminAuthRequest, string, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved ?? false || callback == null)
        {
            return this;
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
        return this;
    }

    private AuthRequestUpdateProcessor Approve()
    {
        if (string.IsNullOrWhiteSpace(_updates.Key))
        {
            throw new ApprovedAuthRequestIsMissingKeyException(_updates.Id);
        }
        ProcessedAuthRequest = _unprocessedAuthRequest;
        ProcessedAuthRequest.Key = _updates.Key;
        ProcessedAuthRequest.Approved = true;
        ProcessedAuthRequest.ResponseDate = DateTime.UtcNow;
        return this;
    }

    private AuthRequestUpdateProcessor Deny()
    {
        ProcessedAuthRequest = _unprocessedAuthRequest;
        ProcessedAuthRequest.Approved = false;
        ProcessedAuthRequest.ResponseDate = DateTime.UtcNow;
        return this;
    }
}
