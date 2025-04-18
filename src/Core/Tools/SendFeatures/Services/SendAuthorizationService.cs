using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Tools.Services;

public class SendAuthorizationService : ISendAuthorizationService
{
    private readonly ISendRepository _sendRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;

    public SendAuthorizationService(
        ISendRepository sendRepository,
        IPasswordHasher<User> passwordHasher,
        IPushNotificationService pushNotificationService,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext)
    {
        _sendRepository = sendRepository;
        _passwordHasher = passwordHasher;
        _pushNotificationService = pushNotificationService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
    }

    public (bool grant, bool passwordRequiredError, bool passwordInvalidError) SendCanBeAccessed(Send send,
        string password)
    {
        var now = DateTime.UtcNow;
        if (send == null || send.MaxAccessCount.GetValueOrDefault(int.MaxValue) <= send.AccessCount ||
            send.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < now || send.Disabled ||
            send.DeletionDate < now)
        {
            return (false, false, false);
        }
        if (!string.IsNullOrWhiteSpace(send.Password))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, true, false);
            }
            var passwordResult = _passwordHasher.VerifyHashedPassword(new User(), send.Password, password);
            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                send.Password = HashPassword(password);
            }
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return (false, false, true);
            }
        }

        return (true, false, false);
    }

    // Response: Send, password required, password invalid
    public async Task<(Send, bool, bool)> AccessAsync(Guid sendId, string password)
    {
        var send = await _sendRepository.GetByIdAsync(sendId);
        var (grantAccess, passwordRequired, passwordInvalid) = SendCanBeAccessed(send, password);

        if (!grantAccess)
        {
            return (null, passwordRequired, passwordInvalid);
        }

        // TODO: maybe move this to a simple ++ sproc?
        if (send.Type != SendType.File)
        {
            // File sends are incremented during file download
            send.AccessCount++;
        }

        await _sendRepository.ReplaceAsync(send);
        await _pushNotificationService.PushSyncSendUpdateAsync(send);
        await _referenceEventService.RaiseEventAsync(new ReferenceEvent
        {
            Id = send.UserId ?? default,
            Type = ReferenceEventType.SendAccessed,
            Source = ReferenceEventSource.User,
            SendType = send.Type,
            MaxAccessCount = send.MaxAccessCount,
            HasPassword = !string.IsNullOrWhiteSpace(send.Password),
            SendHasNotes = send.Data?.Contains("Notes"),
            ClientId = _currentContext.ClientId,
            ClientVersion = _currentContext.ClientVersion
        });
        return (send, false, false);
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new User(), password);
    }
}
