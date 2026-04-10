using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Tools.Services;

public class LocalSendStorageService(
    GlobalSettings globalSettings,
    ISendRepository sendRepository,
    ILogger<LocalSendStorageService> logger) : ISendFileStorageService
{
    private readonly string _baseDirPath = globalSettings.Send.BaseDirectory;
    private readonly string _baseSendUrl = globalSettings.Send.BaseUrl;
    private readonly ISendRepository _sendRepository = sendRepository;
    private readonly ILogger<LocalSendStorageService> _logger = logger;
    private string RelativeFilePath(Send send, string fileID) => $"{send.Id}/{fileID}";
    private string FilePath(Send send, string fileID) => $"{_baseDirPath}/{RelativeFilePath(send, fileID)}";
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
    {
        await InitAsync();
        var path = FilePath(send, fileId);
        // Path.GetDirectoryName will return null for a root path C:\\ or /
        // This is not possible based upon path construction using send & fileId and so ! operator can be used
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var fs = File.Create(path))
        {
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fs);
        }
    }

    public async Task DeleteFileAsync(Send send, string fileId)
    {
        await InitAsync();
        var path = FilePath(send, fileId);
        DeleteFileIfExists(path);
        // Path.GetDirectoryName will return null for a root path C:\\ or /
        // This is not possible based upon path construction using send & fileId and so ! operator can be used
        DeleteDirectoryIfExistsAndEmpty(Path.GetDirectoryName(path)!);
    }

    public async Task DeleteFilesForOrganizationAsync(Guid organizationId)
    {
        await InitAsync();
        var sends = await _sendRepository.GetManyFileSendsByOrganizationIdAsync(organizationId);
        await DeleteFilesForSendsAsync(sends);
    }

    public async Task DeleteFilesForUserAsync(Guid userId)
    {
        await InitAsync();
        var sends = await _sendRepository.GetManyFileSendsByUserIdAsync(userId);
        await DeleteFilesForSendsAsync(sends);
    }

    public async Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId)
    {
        await InitAsync();
        return $"{_baseSendUrl}/{RelativeFilePath(send, fileId)}";
    }

    private async Task DeleteFilesForSendsAsync(ICollection<Send> sends)
    {
        foreach (var send in sends.Where(s => s.Type == SendType.File))
        {
            try
            {
                var data = send.Data != null
                    ? JsonSerializer.Deserialize<SendFileData>(send.Data)
                    : null;
                if (data?.Id != null)
                {
                    await DeleteFileAsync(send, data.Id);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize Send {SendId} data; blob may be orphaned.", send.Id);
            }
        }
    }

    private void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void DeleteDirectoryIfExistsAndEmpty(string path)
    {
        if (Directory.Exists(path) && !Directory.EnumerateFiles(path).Any())
        {
            Directory.Delete(path);
        }
    }

    private Task InitAsync()
    {
        if (!Directory.Exists(_baseDirPath))
        {
            Directory.CreateDirectory(_baseDirPath);
        }

        return Task.FromResult(0);
    }

    public Task<string> GetSendFileUploadUrlAsync(Send send, string fileId)
        => Task.FromResult($"/sends/{send.Id}/file/{fileId}");

    public Task<(bool, long)> ValidateFileAsync(Send send, string fileId, long minimum, long maximum)
    {
        long length = -1;
        var path = FilePath(send, fileId);
        if (!File.Exists(path))
        {
            return Task.FromResult((false, length));
        }

        length = new FileInfo(path).Length;
        var valid = minimum < length && length < maximum;
        return Task.FromResult((valid, length));
    }
}
