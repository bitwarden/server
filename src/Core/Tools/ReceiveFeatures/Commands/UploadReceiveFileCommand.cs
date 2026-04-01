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

public class UploadReceiveFileCommand : IUploadReceiveFileCommand
{
    private readonly IReceiveFileStorageService _receiveFileStorageService;
    private readonly IReceiveRepository _receiveRepository;
    private readonly IReceiveValidationService _receiveValidationService;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<UploadReceiveFileCommand> _logger;

    public UploadReceiveFileCommand(
        IReceiveFileStorageService receiveFileStorageService,
        IReceiveRepository receiveRepository,
        IReceiveValidationService receiveValidationService,
        IPushNotificationService pushNotificationService,
        ILogger<UploadReceiveFileCommand> logger)
    {
        _receiveFileStorageService = receiveFileStorageService;
        _receiveRepository = receiveRepository;
        _receiveValidationService = receiveValidationService;
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    public async Task<(string Url, string FileId)> GetUploadUrlAsync(
        Receive receive, string fileName, long fileLength, string encapsulatedFileContentEncryptionKey)
    {
        if (fileLength < 1)
        {
            throw new BadRequestException("No file data.");
        }

        if (fileLength > SendFileSettingHelper.MAX_FILE_SIZE)
        {
            throw new BadRequestException($"Max file size is {SendFileSettingHelper.MAX_FILE_SIZE_READABLE}.");
        }

        var storageBytesRemaining = await _receiveValidationService.StorageRemainingForReceiveAsync(receive);
        if (storageBytesRemaining < fileLength)
        {
            throw new BadRequestException("Not enough storage available.");
        }

        var fileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);

        try
        {
            var url = await _receiveFileStorageService.GetReceiveFileUploadUrlAsync(receive, fileId);

            var receiveData = JsonSerializer.Deserialize<ReceiveData>(receive.Data) ?? new ReceiveData();
            receiveData.Files.Add(new ReceiveFileData
            {
                Id = fileId,
                FileName = fileName,
                Size = fileLength,
                EncapsulatedFileContentEncryptionKey = encapsulatedFileContentEncryptionKey,
                Validated = false
            });
            receive.Data = JsonSerializer.Serialize(receiveData);
            receive.RevisionDate = DateTime.UtcNow;

            await _receiveRepository.ReplaceAsync(receive);

            return (url, fileId);
        }
        catch
        {
            _logger.LogWarning(
                "Cleaned up file {FileId} from Receive {ReceiveId} because an error occurred when creating the upload URL.",
                fileId, receive.Id);

            await _receiveFileStorageService.DeleteFileAsync(receive, fileId);
            throw;
        }
    }

    public async Task<bool> ValidateFileAsync(Receive receive, string fileId)
    {
        var receiveData = JsonSerializer.Deserialize<ReceiveData>(receive.Data);
        var fileData = receiveData?.Files.FirstOrDefault(f => f.Id == fileId);
        if (fileData == null)
        {
            throw new BadRequestException("Receive does not have file data for the given file ID.");
        }

        var minimum = fileData.Size - SendFileSettingHelper.FILE_SIZE_LEEWAY;
        var maximum = Math.Min(
            fileData.Size + SendFileSettingHelper.FILE_SIZE_LEEWAY,
            SendFileSettingHelper.MAX_FILE_SIZE);

        var (valid, size) = await _receiveFileStorageService.ValidateFileAsync(
            receive, fileId, minimum, maximum);

        if (!valid)
        {
            _logger.LogWarning(
                "File validation failed for Receive {ReceiveId}, file {FileId}. Reported size {Size} was outside expected range ({Minimum} - {Maximum}).",
                receive.Id, fileId, size, minimum, maximum);

            await _receiveFileStorageService.DeleteFileAsync(receive, fileId);
            return false;
        }

        fileData.Size = size;
        fileData.Validated = true;
        receive.Data = JsonSerializer.Serialize(receiveData);
        await _receiveRepository.ReplaceAsync(receive);
        await _pushNotificationService.PushSyncReceiveUpdateAsync(receive);

        return true;
    }
}
