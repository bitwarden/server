using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationAuth.Models;

public class AuthRequestUpdateProcessor<T> where T : AuthRequest
{
    public T ProcessedAuthRequest { get; private set; }

    private T _unprocessedAuthRequest { get; }
    private OrganizationAuthRequestUpdate _updates { get; }
    private AuthRequestUpdateProcessorConfiguration _configuration { get; }

    public AuthRequestUpdateProcessor(
        T authRequest,
        OrganizationAuthRequestUpdate updates,
        AuthRequestUpdateProcessorConfiguration configuration
    )
    {
        _unprocessedAuthRequest = authRequest;
        _updates = updates;
        _configuration = configuration;
    }

    public AuthRequestUpdateProcessor<T> Process()
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
        return _updates.Approved ?
            Approve() :
            Deny();
    }

    public async Task<AuthRequestUpdateProcessor<T>> SendPushNotification(Func<T, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved ?? false || callback == null)
        {
            return this;
        }
        await callback(ProcessedAuthRequest);
        return this;
    }

    public async Task<AuthRequestUpdateProcessor<T>> SendNewDeviceEmail(Func<T, string, Task> callback)
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
            string.IsNullOrWhiteSpace(_unprocessedAuthRequest.RequestDeviceIdentifier) ?
                deviceTypeDisplayName :
                $"{deviceTypeDisplayName} - {_unprocessedAuthRequest.RequestDeviceIdentifier}";
        await callback(ProcessedAuthRequest, deviceTypeAndIdentifierDisplayString);
        return this;
    }

    public async Task<AuthRequestUpdateProcessor<T>> SendEventLog(Func<T, EventType, Task> callback)
    {
        if (!ProcessedAuthRequest?.Approved == null || callback == null)
        {
            return this;
        }
        var eventType = _updates.Approved ?
            EventType.OrganizationUser_ApprovedAuthRequest :
            EventType.OrganizationUser_RejectedAuthRequest;
        await callback(ProcessedAuthRequest, eventType);
        return this;
    }

    private AuthRequestUpdateProcessor<T> Approve()
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

    private AuthRequestUpdateProcessor<T> Deny()
    {
        ProcessedAuthRequest = _unprocessedAuthRequest;
        ProcessedAuthRequest.Approved = false;
        ProcessedAuthRequest.ResponseDate = DateTime.UtcNow;
        return this;
    }
}
