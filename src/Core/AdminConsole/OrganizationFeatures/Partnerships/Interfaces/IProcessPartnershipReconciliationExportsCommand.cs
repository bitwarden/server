namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public record PartnershipReconciliationExportProcessingResult(int Completed, int Failed, int Deleted);

public interface IProcessPartnershipReconciliationExportsCommand
{
    /// <summary>
    /// Deletes expired exports and their files, then writes the file for every pending export and marks it completed,
    /// or failed if writing it throws. An export that fails to delete is logged and skipped.
    /// </summary>
    Task<PartnershipReconciliationExportProcessingResult> ProcessAsync();
}
