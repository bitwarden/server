using System.Text.Json;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.ReceiveFeatures.Commands.Interfaces;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;

namespace Bit.Core.Tools.ReceiveFeatures.Commands;

public class UploadReceiveFileCommand : IUploadReceiveFileCommand
{
    private readonly IReceiveFileStorageService _receiveFileStorageService;
    private readonly IReceiveRepository _receiveRepository;

    public UploadReceiveFileCommand(
        IReceiveFileStorageService receiveFileStorageService,
        IReceiveRepository receiveRepository)
    {
        _receiveFileStorageService = receiveFileStorageService;
        _receiveRepository = receiveRepository;
    }

    public async Task<(string Url, string FileId)> GetUploadUrlAsync(
        Receive receive, string fileName, string encapsulatedFileContentEncryptionKey)
    {
        var fileId = CoreHelpers.SecureRandomString(32, upper: false, special: false);
        var url = await _receiveFileStorageService.GetReceiveFileUploadUrlAsync(receive, fileId);

        var receiveData = JsonSerializer.Deserialize<ReceiveData>(receive.Data) ?? new ReceiveData();
        receiveData.Files.Add(new ReceiveFileData
        {
            Id = fileId,
            FileName = fileName,
            EncapsulatedFileContentEncryptionKey = encapsulatedFileContentEncryptionKey
        });
        receive.Data = JsonSerializer.Serialize(receiveData);
        receive.RevisionDate = DateTime.UtcNow;

        await _receiveRepository.ReplaceAsync(receive);

        return (url, fileId);
    }
}
