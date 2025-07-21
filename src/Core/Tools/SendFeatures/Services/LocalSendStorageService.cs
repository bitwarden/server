// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Tools.Services;

public class LocalSendStorageService : ISendFileStorageService
{
    private readonly string _baseDirPath;
    private readonly string _baseSendUrl;

    private string RelativeFilePath(Send send, string fileID) => $"{send.Id}/{fileID}";
    private string FilePath(Send send, string fileID) => $"{_baseDirPath}/{RelativeFilePath(send, fileID)}";
    public FileUploadType FileUploadType => FileUploadType.Direct;

    public LocalSendStorageService(
        GlobalSettings globalSettings)
    {
        _baseDirPath = globalSettings.Send.BaseDirectory;
        _baseSendUrl = globalSettings.Send.BaseUrl;
    }

    public async Task UploadNewFileAsync(Stream stream, Send send, string fileId)
    {
        await InitAsync();
        var path = FilePath(send, fileId);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
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
        DeleteDirectoryIfExistsAndEmpty(Path.GetDirectoryName(path));
    }

    public async Task DeleteFilesForOrganizationAsync(Guid organizationId)
    {
        await InitAsync();
    }

    public async Task DeleteFilesForUserAsync(Guid userId)
    {
        await InitAsync();
    }

    public async Task<string> GetSendFileDownloadUrlAsync(Send send, string fileId)
    {
        await InitAsync();
        return $"{_baseSendUrl}/{RelativeFilePath(send, fileId)}";
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
        var valid = minimum < length || length < maximum;
        return Task.FromResult((valid, length));
    }
}
