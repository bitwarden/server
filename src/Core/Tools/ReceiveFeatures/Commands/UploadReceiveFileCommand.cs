using System.Text.Json;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class UploadReceiveFileCommand(
    IReceiveFileStorageService receiveFileStorageService,
    IReceiveRepository receiveRepository,
    IReceiveValidationService receiveValidationService,
    IPushNotificationService pushNotificationService,
    ILogger<UploadReceiveFileCommand> logger) : IUploadReceiveFileCommand
{
    public async Task<(string url, string fileId)> GetUploadUrlAsync(Receive receive, string fileName, long fileLength, string encapsulatedFileEncryptionKey)
    {
        if (fileLength < 1)
        {
            throw new BadRequestException("No file data.");
        }

        if (fileLength > SendFileSettingHelper.MAX_FILE_SIZE)
        {
            throw new BadRequestException($"Max file size is {SendFileSettingHelper.MAX_FILE_SIZE_READABLE}.");
        }

        var storageBytesRemaining = await receiveValidationService.StorageRemainingForReceiveAsync(receive);
        if (storageBytesRemaining < fileLength)
        {
            throw new BadRequestException("Not enough storage available.");
        }

        var fileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);

        try
        {
            var fileData = JsonSerializer.Deserialize<ReceiveFileData>(receive.Data) ?? new ReceiveFileData();
            fileData.Id = fileId;
            fileData.FileName = fileName;
            fileData.Size = fileLength;
            fileData.Validated = false;
            fileData.EncapsulatedFileEncryptionKey = encapsulatedFileEncryptionKey;
            receive.Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull);
            receive.UploadCount++;

            await receiveRepository.ReplaceAsync(receive);

            var url = await receiveFileStorageService.GetReceiveFileUploadUrlAsync(receive, fileId);
            return (url, fileId);
        }
        catch
        {
            logger.LogWarning(
                "Cleaned up file {FileId} from Receive {ReceiveId} because an error occurred when creating the upload URL.",
                fileId, receive.Id);

            await receiveFileStorageService.DeleteFileAsync(receive, fileId);
            throw;
        }
    }

    public async Task<bool> ValidateFileAsync(Receive receive)
    {
        var fileData = JsonSerializer.Deserialize<ReceiveFileData>(receive.Data);
        if (fileData?.Id == null)
        {
            throw new BadRequestException("Receive does not have file data.");
        }

        var minimum = fileData.Size - SendFileSettingHelper.FILE_SIZE_LEEWAY;
        var maximum = Math.Min(
            fileData.Size + SendFileSettingHelper.FILE_SIZE_LEEWAY,
            SendFileSettingHelper.MAX_FILE_SIZE);

        var (valid, size) = await receiveFileStorageService.ValidateFileAsync(
            receive, fileData.Id, minimum, maximum);

        if (!valid)
        {
            logger.LogWarning(
                "File validation failed for Receive {ReceiveId}. Reported size {Size} was outside expected range ({Minimum} - {Maximum}).",
                receive.Id, size, minimum, maximum);

            await receiveFileStorageService.DeleteFileAsync(receive, fileData.Id);
            return false;
        }

        fileData.Size = size;
        fileData.Validated = true;
        receive.Data = JsonSerializer.Serialize(fileData, JsonHelpers.IgnoreWritingNull);
        await receiveRepository.ReplaceAsync(receive);
        await pushNotificationService.PushSyncReceiveUpdateAsync(receive);

        return true;
    }
}
