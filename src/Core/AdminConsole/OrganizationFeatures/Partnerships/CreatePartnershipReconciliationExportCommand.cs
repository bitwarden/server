using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class CreatePartnershipReconciliationExportCommand(
    IOrganizationPartnershipReconciliationExportRepository exportRepository,
    GlobalSettings globalSettings,
    TimeProvider timeProvider)
    : ICreatePartnershipReconciliationExportCommand
{
    public async Task<OrganizationPartnershipReconciliationExport> CreateAsync(Guid organizationPartnershipId)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var export = new OrganizationPartnershipReconciliationExport
        {
            OrganizationPartnershipId = organizationPartnershipId,
            Status = PartnershipReconciliationExportStatus.Pending,
            CreationDate = now,
            // Bounds how long an export that never completes is kept.
            ExpirationDate = now.Add(globalSettings.Partnerships.ReconciliationExportRetention),
            RevisionDate = now,
        };
        export.SetNewId();

        return await exportRepository.CreateAsync(export);
    }
}
