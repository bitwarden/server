// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;

namespace Bit.Core.Tools.SendFeatures.Commands;

public class AnonymousSendCommand : IAnonymousSendCommand
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ISendAuthorizationService _sendAuthorizationService;

    public AnonymousSendCommand(
        ISendRepository sendRepository,
        ISendFileStorageService sendFileStorageService,
        IPushNotificationService pushNotificationService,
        ISendAuthorizationService sendAuthorizationService
        )
    {
        _sendRepository = sendRepository;
        _sendFileStorageService = sendFileStorageService;
        _pushNotificationService = pushNotificationService;
        _sendAuthorizationService = sendAuthorizationService;
    }

    // Response: Send, password required, password invalid
    public async Task<(string, SendAccessResult)> GetSendFileDownloadUrlAsync(Send send, string fileId, string password)
    {
        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Can only get a download URL for a file type of Send");
        }

        var result = _sendAuthorizationService.SendCanBeAccessed(send, password);

        if (!result.Equals(SendAccessResult.Granted))
        {
            return (null, result);
        }

        send.AccessCount++;
        await _sendRepository.ReplaceAsync(send);
        await _pushNotificationService.PushSyncSendUpdateAsync(send);
        return (await _sendFileStorageService.GetSendFileDownloadUrlAsync(send, fileId), result);
    }
}
