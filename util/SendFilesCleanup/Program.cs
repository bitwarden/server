using Azure.Storage.Blobs;
using Bit.SendFilesCleanup.Services;
using CommandDotNet;
using Microsoft.Extensions.Logging;

namespace Bit.SendFilesCleanup;

internal class Program
{
    private static int Main(string[] args)
    {
        return new AppRunner<Program>().Run(args);
    }

    [DefaultCommand]
    public async Task<int> Execute(
        [Option("sql-connection", Description = "MSSQL connection string (read-only recommended)")]
        string sqlConnection,
        [Option("blob-connection", Description = "Azure Storage connection string for the Send container")]
        string blobConnection,
        [Option("container", Description = "Blob container name")]
        string container = "sendfiles",
        [Option("output-dir", Description = "Directory for reports and logs")]
        string outputDir = "./output",
        [Option("min-age-hours", Description = "Exclude blobs modified within the last N hours")]
        int minAgeHours = 24,
        [Option("execute", Description = "Actually delete orphans. Without this flag, the tool runs in dry-run mode.")]
        bool execute = false)
    {
        if (string.IsNullOrWhiteSpace(sqlConnection))
        {
            Console.Error.WriteLine("--sql-connection is required");
            return 2;
        }
        if (string.IsNullOrWhiteSpace(blobConnection))
        {
            Console.Error.WriteLine("--blob-connection is required");
            return 2;
        }

        Directory.CreateDirectory(outputDir);

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
                options.UseUtcTimestamp = true;
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var log = loggerFactory.CreateLogger<Program>();

        var allBlobsCsv = Path.Combine(outputDir, "all_blobs.csv");
        var validBlobsTxt = Path.Combine(outputDir, "valid_blobs.txt");
        var orphansTxt = Path.Combine(outputDir, "orphans.txt");
        var orphansSummaryTxt = Path.Combine(outputDir, "orphans_summary.txt");
        var deletedLog = Path.Combine(outputDir, "deleted.log");
        var errorsLog = Path.Combine(outputDir, "errors.log");

        var serviceClient = new BlobServiceClient(blobConnection);
        var containerClient = serviceClient.GetBlobContainerClient(container);

        log.LogInformation("Mode: {Mode}", execute ? "EXECUTE (deletions will occur)" : "DRY RUN");
        log.LogInformation("Container: {Container}  Output dir: {OutputDir}", container, Path.GetFullPath(outputDir));

        // Snapshot the container BEFORE the DB query so any upload landing mid-run
        // is guaranteed to be present in the validBlobList by the time it is read.
        log.LogInformation("Listing blobs in container '{Container}'...", container);
        var lister = new AllBlobsLister(containerClient, loggerFactory.CreateLogger<AllBlobsLister>());
        var allBlobs = await lister.ListAsync(allBlobsCsv);

        log.LogInformation("Loading whitelist from Send table...");
        var validBlobListLoader = new ValidBlobsLoader(sqlConnection, loggerFactory.CreateLogger<ValidBlobsLoader>());
        var validBlobs = await validBlobListLoader.LoadAsync(validBlobsTxt);

        var cutoff = DateTimeOffset.UtcNow.AddHours(-minAgeHours);
        var alreadyDeleted = await OrphanBlobsLister.LoadDeletedLogAsync(deletedLog);
        log.LogInformation("Resume skip list: {Count} previously-deleted entries", alreadyDeleted.Count);

        var blobLister = new OrphanBlobsLister(loggerFactory.CreateLogger<OrphanBlobsLister>());
        var (orphans, summary) = await blobLister.ComputeAsync(
            allBlobs, validBlobs, cutoff, alreadyDeleted, orphansTxt, orphansSummaryTxt);

        log.LogInformation(
            "Summary: all={AllBlobs}  whitelist={Whitelist}  orphans={Orphans}  bytes={Bytes}  oldest={Oldest:O}  newest={Newest:O}",
            allBlobs.Count, validBlobs.Count, summary.Count, summary.TotalBytes, summary.Oldest, summary.Newest);

        if (!execute)
        {
            log.LogInformation("Dry run — no deletions performed. Re-run with --execute to delete.");
            return 0;
        }

        if (orphans.Count == 0)
        {
            log.LogInformation("Nothing to delete.");
            return 0;
        }

        var deleter = new OrphanBlobsDeleter(serviceClient, containerClient, loggerFactory.CreateLogger<OrphanBlobsDeleter>());
        var result = await deleter.DeleteAsync(orphans, deletedLog, errorsLog);

        log.LogInformation(
            "Done. Deleted={Deleted} AlreadyGone={AlreadyGone} Failed={Failed}",
            result.Deleted, result.AlreadyGone, result.Failed);

        return result.Failed == 0 ? 0 : 1;
    }
}
