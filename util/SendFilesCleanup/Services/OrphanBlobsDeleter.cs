using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;

namespace Bit.SendFilesCleanup.Services;

public record DeletionResult(int Deleted, int AlreadyGone, int Failed);

public class OrphanBlobsDeleter
{
    private const int BatchSize = 256;

    private readonly BlobContainerClient _containerClient;
    private readonly BlobBatchClient _batchClient;
    private readonly ILogger<OrphanBlobsDeleter> _logger;

    public OrphanBlobsDeleter(
        BlobServiceClient serviceClient,
        BlobContainerClient containerClient,
        ILogger<OrphanBlobsDeleter> logger)
    {
        _containerClient = containerClient;
        _batchClient = serviceClient.GetBlobBatchClient();
        _logger = logger;
    }

    public async Task<DeletionResult> DeleteAsync(
        IReadOnlyList<BlobInfo> orphans,
        string deletedLogPath,
        string errorsLogPath,
        CancellationToken ct = default)
    {
        if (orphans.Count == 0)
        {
            _logger.LogInformation("No orphans to delete.");
            return new DeletionResult(0, 0, 0);
        }

        var deleted = 0;
        var notFound = 0;
        var failed = 0;
        var processedBatches = 0;

        await using var deletedWriter = new StreamWriter(deletedLogPath, append: true);
        await using var errorsWriter = new StreamWriter(errorsLogPath, append: true);

        foreach (var batch in orphans.Chunk(BatchSize))
        {
            var blobBatch = _batchClient.CreateBatch();
            var perBlob = new List<(BlobInfo Blob, Response Response)>(batch.Length);

            foreach (var blob in batch)
            {
                var uri = _containerClient.GetBlobClient(blob.Name).Uri;
                var response = blobBatch.DeleteBlob(uri);
                perBlob.Add((blob, response));
            }

            try
            {
                await _batchClient.SubmitBatchAsync(blobBatch, throwOnAnyFailure: false, cancellationToken: ct);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex,
                    "Batch submission failed entirely for {Count} blobs; marking all as failed.",
                    batch.Length);
                foreach (var (blob, _) in perBlob)
                {
                    await errorsWriter.WriteLineAsync($"{blob.Name}\tBATCH_SUBMIT_FAILED\t{ex.Status}\t{ex.ErrorCode}");
                    failed++;
                }
                continue;
            }

            foreach (var (blob, response) in perBlob)
            {
                var status = response.Status;
                if (status == 202)
                {
                    await deletedWriter.WriteLineAsync(blob.Name);
                    deleted++;
                }
                else if (status == 404)
                {
                    await deletedWriter.WriteLineAsync(blob.Name);
                    notFound++;
                }
                else
                {
                    await errorsWriter.WriteLineAsync($"{blob.Name}\tSTATUS_{status}");
                    failed++;
                }
            }

            processedBatches++;
            if (processedBatches % 10 == 0)
            {
                _logger.LogInformation(
                    "Progress: {Deleted} deleted, {NotFound} not found, {Failed} failed (batches={Batches})",
                    deleted, notFound, failed, processedBatches);
            }
        }

        _logger.LogInformation(
            "Deletion complete: {Deleted} deleted, {AlreadyGone} already gone, {Failed} failed",
            deleted, notFound, failed);

        return new DeletionResult(deleted, notFound, failed);
    }
}
