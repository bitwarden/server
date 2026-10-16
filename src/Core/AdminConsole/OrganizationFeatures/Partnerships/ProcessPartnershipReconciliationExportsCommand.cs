using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class ProcessPartnershipReconciliationExportsCommand(
    IOrganizationPartnershipReconciliationExportRepository exportRepository,
    IOrganizationPartnershipEntitlementRepository entitlementRepository,
    IPartnershipReconciliationExportStorageService storageService,
    GlobalSettings globalSettings,
    TimeProvider timeProvider,
    ILogger<ProcessPartnershipReconciliationExportsCommand> logger)
    : IProcessPartnershipReconciliationExportsCommand
{
    public const int PageSize = 1000;

    private static readonly byte[] _newLine = "\n"u8.ToArray();

    public static string BlobName(OrganizationPartnershipReconciliationExport export) =>
        $"{export.OrganizationPartnershipId}/{export.Id}.ndjson";

    public async Task<PartnershipReconciliationExportProcessingResult> ProcessAsync()
    {
        var deleted = await DeleteExpiredAsync();

        var completed = 0;
        var failed = 0;
        foreach (var export in await exportRepository.GetManyPendingAsync())
        {
            bool started;
            try
            {
                started = await exportRepository.StartIfPendingAsync(export.Id, Now());
            }
            catch (Exception ex)
            {
                LogFailure(ex, "start", export.Id);
                continue;
            }

            if (!started)
            {
                continue;
            }

            if (await WriteAsync(export))
            {
                completed++;
            }
            else
            {
                failed++;
            }
        }

        return new PartnershipReconciliationExportProcessingResult(completed, failed, deleted);
    }

    private async Task<int> DeleteExpiredAsync()
    {
        var deleted = 0;
        foreach (var export in await exportRepository.GetManyExpiredAsync(Now()))
        {
            try
            {
                // By derived name, so a file left by an interrupted run is removed too.
                await storageService.DeleteAsync(BlobName(export));
                await exportRepository.DeleteAsync(export);
                deleted++;
            }
            catch (Exception ex)
            {
                LogFailure(ex, "delete", export.Id);
            }
        }

        return deleted;
    }

    private async Task<bool> WriteAsync(OrganizationPartnershipReconciliationExport export)
    {
        var blobName = BlobName(export);
        try
        {
            var recordCount = 0;
            await using (var stream = await storageService.OpenWriteAsync(blobName))
            {
                Guid? afterId = null;
                ICollection<OrganizationPartnershipEntitlement> page;
                do
                {
                    page = await entitlementRepository.GetPageByOrganizationPartnershipIdAsync(
                        export.OrganizationPartnershipId, afterId, PageSize);
                    foreach (var entitlement in page)
                    {
                        await JsonSerializer.SerializeAsync(stream,
                            PartnershipReconciliationExportRecord.From(entitlement),
                            PartnershipReconciliationExportRecord.SerializerOptions);
                        await stream.WriteAsync(_newLine);
                        recordCount++;
                        afterId = entitlement.Id;
                    }
                } while (page.Count == PageSize);
            }

            var now = Now();
            export.Status = PartnershipReconciliationExportStatus.Completed;
            export.RecordCount = recordCount;
            export.BlobName = blobName;
            export.CompletionDate = now;
            export.ExpirationDate = now.Add(globalSettings.Partnerships.ReconciliationExportRetention);
            export.RevisionDate = now;
            await exportRepository.ReplaceAsync(export);
            return true;
        }
        catch (Exception ex)
        {
            LogFailure(ex, "write", export.Id);
            await MarkFailedAsync(export, blobName);
            return false;
        }
    }

    private async Task MarkFailedAsync(OrganizationPartnershipReconciliationExport export, string blobName)
    {
        try
        {
            // Disposing the write stream commits whatever was written before the failure.
            await storageService.DeleteAsync(blobName);

            var now = Now();
            export.Status = PartnershipReconciliationExportStatus.Failed;
            export.RecordCount = null;
            export.BlobName = null;
            export.CompletionDate = now;
            export.ExpirationDate = now.Add(globalSettings.Partnerships.ReconciliationExportRetention);
            export.RevisionDate = now;
            await exportRepository.ReplaceAsync(export);
        }
        catch (Exception ex)
        {
            LogFailure(ex, "mark failed", export.Id);
        }
    }

    // Exception messages can carry entitlement data such as external IDs, so only the type and stack are logged.
    private void LogFailure(Exception ex, string operation, Guid exportId) =>
        logger.LogError(
            "Partnership reconciliation export {ExportId} failed to {Operation}: {ExceptionType}{NewLine}{StackTrace}",
            exportId, operation, ex.GetType().FullName, Environment.NewLine, ex.StackTrace);

    private DateTime Now() => timeProvider.GetUtcNow().UtcDateTime;
}
