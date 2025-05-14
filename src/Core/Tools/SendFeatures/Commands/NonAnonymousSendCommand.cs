using System.Text.Json;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Business;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Commands.Interfaces;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.SendFeatures.Commands;

public class NonAnonymousSendCommand : INonAnonymousSendCommand
{
    private readonly ISendRepository _sendRepository;
    private readonly ISendFileStorageService _sendFileStorageService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ISendValidationService _sendValidationService;
    private readonly IReferenceEventService _referenceEventService;
    private readonly ICurrentContext _currentContext;
    private readonly ISendCoreHelperService _sendCoreHelperService;
    private readonly ISendFileSettingHelper _sendFileSettingHelper;

    public NonAnonymousSendCommand(
        ISendRepository sendRepository,
        ISendFileStorageService sendFileStorageService,
        IPushNotificationService pushNotificationService,
        ISendValidationService sendValidationService,
        IReferenceEventService referenceEventService,
        ICurrentContext currentContext,
        ISendCoreHelperService sendCoreHelperService,
        ISendFileSettingHelper sendFileSettingHelper
        )
    {
        _sendRepository = sendRepository;
        _sendFileStorageService = sendFileStorageService;
        _pushNotificationService = pushNotificationService;
        _sendValidationService = sendValidationService;
        _referenceEventService = referenceEventService;
        _currentContext = currentContext;
        _sendCoreHelperService = sendCoreHelperService;
        _sendFileSettingHelper = sendFileSettingHelper;
    }

    public async Task SaveSendAsync(Send send)
    {
        // Make sure user can save Sends
        await _sendValidationService.ValidateUserCanSaveAsync(send.UserId, send);

        if (send.Id == default(Guid))
        {
            await _sendRepository.CreateAsync(send);
            await _pushNotificationService.PushSyncSendCreateAsync(send);
            await _referenceEventService.RaiseEventAsync(new ReferenceEvent
            {
                Id = send.UserId ?? default,
                Type = ReferenceEventType.SendCreated,
                Source = ReferenceEventSource.User,
                SendType = send.Type,
                MaxAccessCount = send.MaxAccessCount,
                HasPassword = !string.IsNullOrWhiteSpace(send.Password),
                SendHasNotes = send.Data?.Contains("Notes"),
                ClientId = _currentContext.ClientId,
                ClientVersion = _currentContext.ClientVersion
            });
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
            send.Data = JsonSerializer.Serialize(data,
                JsonHelpers.IgnoreWritingNull);
            await SaveSendAsync(send);
            return await _sendFileStorageService.GetSendFileUploadUrlAsync(send, fileId);
        }
        catch
        {
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

        if (!await UpdateSendOnValidation(send))
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

    public async Task<bool> UpdateSendOnValidation(Send send)
    {
        var fileData = JsonSerializer.Deserialize<SendFileData>(send.Data);

        var (valid, realSize) = await _sendFileStorageService.ValidateFileAsync(send, fileData.Id, fileData.Size, _sendFileSettingHelper.GetFileSizeLeeway());

        if (!valid || realSize > _sendFileSettingHelper.GetMaxFileSize())
        {
            // File reported differs in size from that promised. Must be a rogue client. Delete Send
            await DeleteSendAsync(send);
            return false;
        }

        // Update Send data if necessary
        if (realSize != fileData.Size)
        {
            fileData.Size = realSize.Value;
        }
        fileData.Validated = true;
        send.Data = JsonSerializer.Serialize(fileData,
            JsonHelpers.IgnoreWritingNull);
        await SaveSendAsync(send);

        return valid;
    }

}
