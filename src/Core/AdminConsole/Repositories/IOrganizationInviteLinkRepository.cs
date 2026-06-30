using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationInviteLinkRepository : IRepository<OrganizationInviteLink, Guid>
{
    /// <summary>
    /// Gets an organization invite link by its code.
    /// </summary>
    /// <param name="code">The code of the organization invite link.</param>
    /// <returns>The organization invite link if found, otherwise null.</returns>
    Task<OrganizationInviteLink?> GetByCodeAsync(Guid code);

    /// <summary>
    /// Gets an organization invite link by its organization ID.
    /// </summary>
    /// <param name="organizationId">The ID of the organization.</param>
    /// <returns>The organization invite link if found, otherwise null.</returns>
    Task<OrganizationInviteLink?> GetByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Atomically deletes <paramref name="oldLink"/> and inserts <paramref name="newLink"/> in a single transaction.
    /// If the transaction fails (e.g. constraint violation on the insert), the delete is rolled back so the
    /// organization is never left without an invite link mid-operation.
    /// </summary>
    Task RefreshAsync(OrganizationInviteLink oldLink, OrganizationInviteLink newLink);
}
