// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.PhishingDomainFeatures;

public class AzurePhishingDomainStorageService
{
    private const string _containerName = "phishingdomains";
    private const string _domainsFileName = "domains.txt";
    private const string _checksumFileName = "checksum.txt";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzurePhishingDomainStorageService> _logger;
    private BlobContainerClient _containerClient;

    public AzurePhishingDomainStorageService(
        GlobalSettings globalSettings,
        ILogger<AzurePhishingDomainStorageService> logger)
    {
        _blobServiceClient = new BlobServiceClient(globalSettings.Storage.ConnectionString);
        _logger = logger;
    }

    public async Task<ICollection<string>> GetDomainsAsync()
    {
        await InitAsync();

        var blobClient = _containerClient.GetBlobClient(_domainsFileName);
        if (!await blobClient.ExistsAsync())
        {
            return [];
        }

        var response = await blobClient.DownloadAsync();
        using var streamReader = new StreamReader(response.Value.Content);
        var content = await streamReader.ReadToEndAsync();

        return [.. content
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))];
    }

    public async Task<string> GetChecksumAsync()
    {
        await InitAsync();

        var blobClient = _containerClient.GetBlobClient(_checksumFileName);
        if (!await blobClient.ExistsAsync())
        {
            return string.Empty;
        }

        var response = await blobClient.DownloadAsync();
        using var streamReader = new StreamReader(response.Value.Content);
        return (await streamReader.ReadToEndAsync()).Trim();
    }

    public async Task UpdateDomainsAsync(IEnumerable<string> domains, string checksum)
    {
        await InitAsync();

        var domainsContent = string.Join(Environment.NewLine, domains);
        var domainsStream = new MemoryStream(Encoding.UTF8.GetBytes(domainsContent));
        var domainsBlobClient = _containerClient.GetBlobClient(_domainsFileName);

        await domainsBlobClient.UploadAsync(domainsStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" }
        }, CancellationToken.None);

        var checksumStream = new MemoryStream(Encoding.UTF8.GetBytes(checksum));
        var checksumBlobClient = _containerClient.GetBlobClient(_checksumFileName);

        await checksumBlobClient.UploadAsync(checksumStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" }
        }, CancellationToken.None);
    }

    private async Task InitAsync()
    {
        if (_containerClient is null)
        {
            _containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await _containerClient.CreateIfNotExistsAsync();
        }
    }
}
