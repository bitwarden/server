using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Bit.SendFilesCleanup.Services;

public record OrphanSummary(int Count, long TotalBytes, DateTimeOffset? Oldest, DateTimeOffset? Newest);

public class OrphanBlobsLister
{
    private readonly ILogger<OrphanBlobsLister> _logger;

    public OrphanBlobsLister(ILogger<OrphanBlobsLister> logger)
    {
        _logger = logger;
    }

    public async Task<(IReadOnlyList<BlobInfo> Orphans, OrphanSummary Summary)> ComputeAsync(
        IReadOnlyList<BlobInfo> allBlobs,
        HashSet<string> validBlobs,
        DateTimeOffset cutoff,
        HashSet<string> alreadyDeleted,
        string orphansTxtPath,
        string summaryTxtPath)
    {
        var orphans = new List<BlobInfo>();

        foreach (var blob in allBlobs)
        {
            if (validBlobs.Contains(blob.Name))
            {
                continue;
            }
            if (blob.LastModified >= cutoff)
            {
                continue;
            }
            if (alreadyDeleted.Contains(blob.Name))
            {
                continue;
            }
            orphans.Add(blob);
        }

        await using (var writer = new StreamWriter(orphansTxtPath))
        {
            foreach (var orphan in orphans)
            {
                await writer.WriteLineAsync(orphan.Name);
            }
        }

        var totalBytes = orphans.Sum(o => o.ContentLength);
        DateTimeOffset? oldest = orphans.Count == 0 ? null : orphans.Min(o => o.LastModified);
        DateTimeOffset? newest = orphans.Count == 0 ? null : orphans.Max(o => o.LastModified);
        var summary = new OrphanSummary(orphans.Count, totalBytes, oldest, newest);

        await using (var writer = new StreamWriter(summaryTxtPath))
        {
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"OrphanCount={summary.Count}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"TotalBytes={summary.TotalBytes}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Oldest={summary.Oldest:O}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"Newest={summary.Newest:O}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"CutoffUtc={cutoff:O}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"AllBlobs={allBlobs.Count}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"WhitelistSize={validBlobs.Count}"));
            await writer.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"AlreadyDeletedSkipped={alreadyDeleted.Count}"));
        }

        _logger.LogInformation(
            "Orphans computed: {Count} ({Bytes} bytes), cutoff={Cutoff:O}",
            summary.Count, summary.TotalBytes, cutoff);

        return (orphans, summary);
    }

    public static async Task<HashSet<string>> LoadDeletedLogAsync(string deletedLogPath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(deletedLogPath))
        {
            return set;
        }
        using var reader = new StreamReader(deletedLogPath);
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                set.Add(line.Trim());
            }
        }
        return set;
    }
}
