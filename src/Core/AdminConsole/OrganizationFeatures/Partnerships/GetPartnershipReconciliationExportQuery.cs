using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Services;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class GetPartnershipReconciliationExportQuery(
    IOrganizationPartnershipReconciliationExportRepository exportRepository,
    IPartnershipReconciliationExportStorageService storageService,
    TimeProvider timeProvider)
    : IGetPartnershipReconciliationExportQuery
{
    public async Task<CommandResult<PartnershipReconciliationExportDetails>> GetAsync(
        Guid organizationPartnershipId, Guid exportId)
    {
        var export = await exportRepository.GetByIdAsync(exportId);
        if (export is null || export.OrganizationPartnershipId != organizationPartnershipId)
        {
            return new ReconciliationExportNotFound();
        }

        var downloadable = export is { Status: PartnershipReconciliationExportStatus.Completed, BlobName: not null } &&
                           export.ExpirationDate > timeProvider.GetUtcNow().UtcDateTime;
        var downloadUrl = downloadable ? await storageService.GetDownloadUrlAsync(export.BlobName!) : null;

        return new PartnershipReconciliationExportDetails(export, downloadUrl);
    }
}
