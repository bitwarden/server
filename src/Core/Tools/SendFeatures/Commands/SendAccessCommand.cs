using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;

namespace Bit.Core.Tools.SendFeatures.Commands;

public class SendAccessCommand : ISendAccessCommand
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;

    public SendAccessCommand(
        ISendRepository sendRepository,
        ISendFileStorageService sendFileStorageService,
        IPushNotificationService pushNotificationService)
    {
        _sendRepository = sendRepository;
        _sendFileStorageService = sendFileStorageService;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<(string?, SendAccessResult)> GetSendFileDownloadUrlAsync(Send send, string fileId)
    {
        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Can only get a download URL for a file type of Send");
        }

        if (!ISendAccessCommand.SendCanBeAccessed(send))
        {
            return (null, SendAccessResult.Denied);
        }

        var fileData = JsonSerializer.Deserialize<SendFileData>(send.Data ?? string.Empty);
        if (fileData?.Id != fileId)
        {
            throw new NotFoundException();
        }

        await IncrementAccessCountAsync(send);
        return (await _sendFileStorageService.GetSendFileDownloadUrlAsync(send, fileId), SendAccessResult.Granted);
    }

    public async Task IncrementAccessCountAsync(Send send)
    {
        send.AccessCount++;
        await _sendRepository.ReplaceAsync(send);
        await _pushNotificationService.PushSyncSendUpdateAsync(send);
    }
}
