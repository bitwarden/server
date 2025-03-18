using System.Text;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.PhishingDomainFeatures;

public class AzurePhishingDomainStorageService
{
    public const string ContainerName = "phishingdomains";
    public const string DomainsFileName = "domains.txt";
    public const string ChecksumFileName = "checksum.txt";

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

        var blobClient = _containerClient.GetBlobClient(DomainsFileName);
        if (!await blobClient.ExistsAsync())
        {
            return new List<string>();
        }

        var response = await blobClient.DownloadAsync();
        using var streamReader = new StreamReader(response.Value.Content);
        var content = await streamReader.ReadToEndAsync();

        return content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            .ToList();
    }

    public async Task<string> GetChecksumAsync()
    {
        await InitAsync();

        var blobClient = _containerClient.GetBlobClient(ChecksumFileName);
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

        // Upload domains
        var domainsContent = string.Join(Environment.NewLine, domains);
        var domainsStream = new MemoryStream(Encoding.UTF8.GetBytes(domainsContent));
        var domainsBlobClient = _containerClient.GetBlobClient(DomainsFileName);

        await domainsBlobClient.UploadAsync(domainsStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" }
        }, default);

        // Upload checksum
        var checksumStream = new MemoryStream(Encoding.UTF8.GetBytes(checksum));
        var checksumBlobClient = _containerClient.GetBlobClient(ChecksumFileName);

        await checksumBlobClient.UploadAsync(checksumStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain" }
        }, default);
    }

    private async Task InitAsync()
    {
        if (_containerClient == null)
        {
            _containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, null, null);
        }
    }
}
