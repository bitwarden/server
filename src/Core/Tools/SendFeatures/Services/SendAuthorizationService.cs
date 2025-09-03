using Bit.Core.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Tools.Services;

public class SendAuthorizationService : ISendAuthorizationService
{
    private readonly ISendRepository _sendRepository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPushNotificationService _pushNotificationService;

    public SendAuthorizationService(
        ISendRepository sendRepository,
        IPasswordHasher<User> passwordHasher,
        IPushNotificationService pushNotificationService)
    {
        _sendRepository = sendRepository;
        _passwordHasher = passwordHasher;
        _pushNotificationService = pushNotificationService;
    }

    public SendAccessResult SendCanBeAccessed(Send send,
        string password)
    {
        var now = DateTime.UtcNow;
        if (send == null || send.MaxAccessCount.GetValueOrDefault(int.MaxValue) <= send.AccessCount ||
            send.ExpirationDate.GetValueOrDefault(DateTime.MaxValue) < now || send.Disabled ||
            send.DeletionDate < now)
        {
            return SendAccessResult.Denied;
        }
        if (!string.IsNullOrWhiteSpace(send.Password))
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return SendAccessResult.PasswordRequired;
            }
            var passwordResult = _passwordHasher.VerifyHashedPassword(new User(), send.Password, password);
            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                send.Password = HashPassword(password);
            }
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return SendAccessResult.PasswordInvalid;
            }
        }

        return SendAccessResult.Granted;
    }

    public async Task<SendAccessResult> AccessAsync(Send sendToBeAccessed, string password)
    {
        var accessResult = SendCanBeAccessed(sendToBeAccessed, password);

        if (!accessResult.Equals(SendAccessResult.Granted))
        {
            return accessResult;
        }

        if (sendToBeAccessed.Type != SendType.File)
        {
            // File sends are incremented during file download
            sendToBeAccessed.AccessCount++;
        }

        await _sendRepository.ReplaceAsync(sendToBeAccessed);
        await _pushNotificationService.PushSyncSendUpdateAsync(sendToBeAccessed);
        return accessResult;
    }

    public string HashPassword(string password)
    {
        return _passwordHasher.HashPassword(new User(), password);
    }
}
