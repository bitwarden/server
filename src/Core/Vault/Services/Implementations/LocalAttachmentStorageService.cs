using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class LocalAttachmentStorageService : IAttachmentStorageService
{
    private readonly string _baseAttachmentUrl;
    private readonly string _baseDirPath;
    private readonly string _baseTempDirPath;

    public FileUploadType FileUploadType => FileUploadType.Direct;

    public LocalAttachmentStorageService(
        IGlobalSettings globalSettings)
    {
        _baseDirPath = globalSettings.Attachment.BaseDirectory;
        _baseTempDirPath = $"{_baseDirPath}/temp";
        _baseAttachmentUrl = globalSettings.Attachment.BaseUrl;
    }

    public async Task<string> GetAttachmentDownloadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync();
        return $"{_baseAttachmentUrl}/{cipher.Id}/{attachmentData.AttachmentId}";
    }

    public async Task UploadNewAttachmentAsync(Stream stream, Cipher cipher, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync();
        var cipherDirPath = CipherDirectoryPath(cipher.Id, temp: false);
        CreateDirectoryIfNotExists(cipherDirPath);

        using (var fs = File.Create(AttachmentFilePath(cipherDirPath, attachmentData.AttachmentId)))
        {
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fs);
        }
    }

    public async Task UploadShareAttachmentAsync(Stream stream, Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync();
        var tempCipherOrgDirPath = OrganizationDirectoryPath(cipherId, organizationId, temp: true);
        CreateDirectoryIfNotExists(tempCipherOrgDirPath);

        using (var fs = File.Create(AttachmentFilePath(tempCipherOrgDirPath, attachmentData.AttachmentId)))
        {
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fs);
        }
    }

    public async Task StartShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync();
        var sourceFilePath = AttachmentFilePath(attachmentData.AttachmentId, cipherId, organizationId, temp: true);
        if (!File.Exists(sourceFilePath))
        {
            return;
        }

        var destFilePath = AttachmentFilePath(attachmentData.AttachmentId, cipherId, temp: false);
        if (!File.Exists(destFilePath))
        {
            return;
        }

        var originalFilePath = AttachmentFilePath(attachmentData.AttachmentId, cipherId, temp: true);
        DeleteFileIfExists(originalFilePath);

        File.Move(destFilePath, originalFilePath);
        DeleteFileIfExists(destFilePath);

        File.Move(sourceFilePath, destFilePath);
    }

    public async Task RollbackShareAttachmentAsync(Guid cipherId, Guid organizationId, CipherAttachment.MetaData attachmentData, string originalContainer)
    {
        await InitAsync();
        DeleteFileIfExists(AttachmentFilePath(attachmentData.AttachmentId, cipherId, organizationId, temp: true));

        var originalFilePath = AttachmentFilePath(attachmentData.AttachmentId, cipherId, temp: true);
        if (!File.Exists(originalFilePath))
        {
            return;
        }

        var destFilePath = AttachmentFilePath(attachmentData.AttachmentId, cipherId, temp: false);
        DeleteFileIfExists(destFilePath);

        File.Move(originalFilePath, destFilePath);
        DeleteFileIfExists(originalFilePath);
    }

    public async Task DeleteAttachmentAsync(Guid cipherId, CipherAttachment.MetaData attachmentData)
    {
        await InitAsync();
        DeleteFileIfExists(AttachmentFilePath(attachmentData.AttachmentId, cipherId, temp: false));
    }

    public async Task CleanupAsync(Guid cipherId)
    {
        await InitAsync();
        DeleteDirectoryIfExists(CipherDirectoryPath(cipherId, temp: true));
    }

    public async Task DeleteAttachmentsForCipherAsync(Guid cipherId)
    {
        await InitAsync();
        DeleteDirectoryIfExists(CipherDirectoryPath(cipherId, temp: false));
    }

    public async Task DeleteAttachmentsForOrganizationAsync(Guid organizationId)
    {
        await InitAsync();
    }

    public async Task DeleteAttachmentsForUserAsync(Guid userId)
    {
        await InitAsync();
    }

    private void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private void CreateDirectoryIfNotExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private Task InitAsync()
    {
        if (!Directory.Exists(_baseDirPath))
        {
            Directory.CreateDirectory(_baseDirPath);
        }

        if (!Directory.Exists(_baseTempDirPath))
        {
            Directory.CreateDirectory(_baseTempDirPath);
        }

        return Task.FromResult(0);
    }

    private string CipherDirectoryPath(Guid cipherId, bool temp = false) =>
        Path.Combine(temp ? _baseTempDirPath : _baseDirPath, cipherId.ToString());
    private string OrganizationDirectoryPath(Guid cipherId, Guid organizationId, bool temp = false) =>
        Path.Combine(temp ? _baseTempDirPath : _baseDirPath, cipherId.ToString(), organizationId.ToString());

    private string AttachmentFilePath(string dir, string attachmentId) => Path.Combine(dir, attachmentId);
    private string AttachmentFilePath(string attachmentId, Guid cipherId, Guid? organizationId = null,
        bool temp = false) =>
        organizationId.HasValue ?
        AttachmentFilePath(OrganizationDirectoryPath(cipherId, organizationId.Value, temp), attachmentId) :
        AttachmentFilePath(CipherDirectoryPath(cipherId, temp), attachmentId);
    public Task<string> GetAttachmentUploadUrlAsync(Cipher cipher, CipherAttachment.MetaData attachmentData)
        => Task.FromResult($"{cipher.Id}/attachment/{attachmentData.AttachmentId}");

    public Task<(bool, long?)> ValidateFileAsync(Cipher cipher, CipherAttachment.MetaData attachmentData, long leeway)
    {
        long? length = null;
        var path = AttachmentFilePath(attachmentData.AttachmentId, cipher.Id, temp: false);
        if (!File.Exists(path))
        {
            return Task.FromResult((false, length));
        }

        length = new FileInfo(path).Length;
        if (attachmentData.Size < length - leeway || attachmentData.Size > length + leeway)
        {
            return Task.FromResult((false, length));
        }

        return Task.FromResult((true, length));
    }
}
