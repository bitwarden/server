// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.SendFeatures.Commands;

public class NonAnonymousSendCommand : INonAnonymousSendCommand
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ISendValidationService _sendValidationService;
    private readonly ISendCoreHelperService _sendCoreHelperService;
    private readonly ILogger<NonAnonymousSendCommand> _logger;

    public NonAnonymousSendCommand(ISendRepository sendRepository,
        ISendFileStorageService sendFileStorageService,
        IPushNotificationService pushNotificationService,
        ISendAuthorizationService sendAuthorizationService,
        ISendValidationService sendValidationService,
        ISendCoreHelperService sendCoreHelperService,
        ILogger<NonAnonymousSendCommand> logger)
    {
        _sendRepository = sendRepository;
        _sendFileStorageService = sendFileStorageService;
        _pushNotificationService = pushNotificationService;
        _sendValidationService = sendValidationService;
        _sendCoreHelperService = sendCoreHelperService;
        _logger = logger;
    }

    public async Task SaveSendAsync(Send send)
    {
        // Make sure user can save Sends
        await _sendValidationService.ValidateUserCanSaveAsync(send.UserId, send);

        if (send.Id == default(Guid))
        {
            await _sendRepository.CreateAsync(send);
            await _pushNotificationService.PushSyncSendCreateAsync(send);
        }
        else
        {
            send.RevisionDate = DateTime.UtcNow;
            await _sendRepository.UpsertAsync(send);
            await _pushNotificationService.PushSyncSendUpdateAsync(send);
        }
    }

    public async Task<string> SaveFileSendAsync(Send send, SendFileData data, long fileLength)
    {
        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Send is not of type \"file\".");
        }

        if (fileLength < 1)
        {
            throw new BadRequestException("No file data.");
        }

        if (fileLength > SendFileSettingHelper.MAX_FILE_SIZE)
        {
            throw new BadRequestException($"Max file size is {SendFileSettingHelper.MAX_FILE_SIZE_READABLE}.");
        }

        var storageBytesRemaining = await _sendValidationService.StorageRemainingForSendAsync(send);

        if (storageBytesRemaining < fileLength)
        {
            throw new BadRequestException("Not enough storage available.");
        }

        var fileId = _sendCoreHelperService.SecureRandomString(32, useUpperCase: false, useSpecial: false);

        try
        {
            data.Id = fileId;
            data.Size = fileLength;
            data.Validated = false;
            send.Data = JsonSerializer.Serialize(data, JsonHelpers.IgnoreWritingNull);
            await SaveSendAsync(send);
            return await _sendFileStorageService.GetSendFileUploadUrlAsync(send, fileId);
        }
        catch
        {
            _logger.LogWarning(
                "Deleted file from {SendId} because an error occurred when creating the upload URL.",
                send.Id
            );

            // Clean up since this is not transactional
            await _sendFileStorageService.DeleteFileAsync(send, fileId);
            throw;
        }
    }
    public async Task UploadFileToExistingSendAsync(Stream stream, Send send)
    {
        if (stream.Position > 0)
        {
            stream.Position = 0;
        }

        if (send?.Data == null)
        {
            throw new BadRequestException("Send does not have file data");
        }

        if (send.Type != SendType.File)
        {
            throw new BadRequestException("Not a File Type Send.");
        }

        var data = JsonSerializer.Deserialize<SendFileData>(send.Data);

        if (data.Validated)
        {
            throw new BadRequestException("File has already been uploaded.");
        }

        await _sendFileStorageService.UploadNewFileAsync(stream, send, data.Id);

        if (!await ConfirmFileSize(send))
        {
            throw new BadRequestException("File received does not match expected file length.");
        }
    }
    public async Task DeleteSendAsync(Send send)
    {
        await _sendRepository.DeleteAsync(send);
        if (send.Type == Enums.SendType.File)
        {
            var data = JsonSerializer.Deserialize<SendFileData>(send.Data);
            await _sendFileStorageService.DeleteFileAsync(send, data.Id);
        }
        await _pushNotificationService.PushSyncSendDeleteAsync(send);
    }

    public async Task<bool> ConfirmFileSize(Send send)
    {
        var fileData = JsonSerializer.Deserialize<SendFileData>(send.Data);

        var minimum = fileData.Size - SendFileSettingHelper.FILE_SIZE_LEEWAY;
        var maximum = Math.Min(
            fileData.Size + SendFileSettingHelper.FILE_SIZE_LEEWAY,
            SendFileSettingHelper.MAX_FILE_SIZE
        );
        var (valid, size) = await _sendFileStorageService.ValidateFileAsync(send, fileData.Id, minimum, maximum);

        // protect file service from upload hijacking by deleting invalid sends
        if (!valid)
        {
            _logger.LogWarning(
                "Deleted {SendId} because its reported size {Size} was outside the expected range ({Minimum} - {Maximum}).",
                send.Id,
                size,
                minimum,
                maximum
            );
            await DeleteSendAsync(send);
            return false;
        }

        // replace expected size with validated size
        fileData.Size = size;
        fileData.Validated = true;
        send.Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull);
        await SaveSendAsync(send);

        return valid;
    }

}
