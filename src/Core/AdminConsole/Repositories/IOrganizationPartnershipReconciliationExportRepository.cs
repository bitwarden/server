using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums.Partnerships;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationPartnershipReconciliationExportRepository
    : IRepository<OrganizationPartnershipReconciliationExport, Guid>
{
    /// <summary>
    /// Exports in <see cref="PartnershipReconciliationExportStatus.Pending"/>, oldest first.
    /// </summary>
    Task<ICollection<OrganizationPartnershipReconciliationExport>> GetManyPendingAsync();

    /// <summary>
    /// Exports whose ExpirationDate is at or before <paramref name="asOf"/>, in any status.
    /// </summary>
    Task<ICollection<OrganizationPartnershipReconciliationExport>> GetManyExpiredAsync(DateTime asOf);

    /// <summary>
    /// Moves the export from Pending to Running only if it is still Pending, so concurrent workers never process the
    /// same export twice.
    /// </summary>
    /// <returns>True if this caller claimed the export.</returns>
    Task<bool> StartIfPendingAsync(Guid id, DateTime revisionDate);
}
