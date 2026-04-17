using System.Globalization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace Bit.SendFilesCleanup.Services;

public record BlobInfo(string Name, DateTimeOffset LastModified, long ContentLength);

public class AllBlobsLister
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AllBlobsLister> _logger;

    public AllBlobsLister(BlobContainerClient containerClient, ILogger<AllBlobsLister> logger)
    {
        _containerClient = containerClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BlobInfo>> ListAsync(string outputCsvPath, CancellationToken ct = default)
    {
        var blobs = new List<BlobInfo>();

        await using var writer = new StreamWriter(outputCsvPath);
        await writer.WriteLineAsync("Name,LastModified,ContentLength");

        var blobCount = 0;
        await foreach (var item in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, cancellationToken: ct))
        {
            var lastModified = item.Properties.LastModified ?? DateTimeOffset.MinValue;
            var contentLength = item.Properties.ContentLength ?? 0L;
            blobs.Add(new BlobInfo(item.Name, lastModified, contentLength));

            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture,
                $"{EscapeCsv(item.Name)},{lastModified:O},{contentLength}"));

            if (++blobCount % 10_000 == 0)
            {
                _logger.LogInformation("Enumerated {Count} blobs so far...", blobCount);
            }
        }

        _logger.LogInformation("Enumeration complete. Total blobs: {Count}", blobs.Count);
        return blobs;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
