using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.BulkEnablePam;

public interface IBulkEnablePamCommand
{
    /// <summary>
    /// Grants PAM access to the given members of <paramref name="organization"/>. Members of other organizations and
    /// members who already have access are skipped. Fails when no member is left to grant, when the organization does
    /// not use PAM, or on a self-hosted instance when the grant would exceed the organization's Privileged Controls
    /// seats, since a self-hosted instance cannot add seats.
    /// </summary>
    Task<CommandResult> BulkEnablePamAsync(Organization organization, IEnumerable<Guid> organizationUserIds);
}
