namespace Bit.Core.AdminConsole.Services;

/// <summary>
/// Stores partnership reconciliation export files in private storage reachable only through short-lived URLs.
/// </summary>
public interface IPartnershipReconciliationExportStorageService
{
    /// <summary>
    /// Opens a stream that writes the file at <paramref name="blobName"/>, replacing any existing file. The file is
    /// committed when the stream is disposed.
    /// </summary>
    Task<Stream> OpenWriteAsync(string blobName);

    /// <summary>
    /// A read-only URL for the file that expires after the configured download URL lifetime, or null when export
    /// storage is not configured.
    /// </summary>
    Task<string?> GetDownloadUrlAsync(string blobName);

    Task DeleteAsync(string blobName);
}
