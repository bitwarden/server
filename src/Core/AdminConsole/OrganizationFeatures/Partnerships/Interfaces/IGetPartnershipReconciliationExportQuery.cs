using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

/// <param name="DownloadUrl">A freshly issued short-lived URL, present only for a completed, unexpired export.</param>
public record PartnershipReconciliationExportDetails(
    OrganizationPartnershipReconciliationExport Export, string? DownloadUrl);

public interface IGetPartnershipReconciliationExportQuery
{
    /// <returns>The export, or <see cref="ReconciliationExportNotFound"/> when it doesn't exist or belongs to another
    /// partnership.</returns>
    Task<CommandResult<PartnershipReconciliationExportDetails>> GetAsync(Guid organizationPartnershipId, Guid exportId);
}
