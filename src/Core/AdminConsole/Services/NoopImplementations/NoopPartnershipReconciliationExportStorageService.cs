namespace Bit.Core.AdminConsole.Services.NoopImplementations;

public class NoopPartnershipReconciliationExportStorageService : IPartnershipReconciliationExportStorageService
{
    public Task<Stream> OpenWriteAsync(string blobName) => Task.FromResult(Stream.Null);

    public Task<string?> GetDownloadUrlAsync(string blobName) => Task.FromResult<string?>(null);

    public Task DeleteAsync(string blobName) => Task.CompletedTask;
}
