using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface ICreatePartnershipReconciliationExportCommand
{
    /// <summary>
    /// Queues a pending export of the partnership's entitlements for background processing.
    /// </summary>
    Task<OrganizationPartnershipReconciliationExport> CreateAsync(Guid organizationPartnershipId);
}
