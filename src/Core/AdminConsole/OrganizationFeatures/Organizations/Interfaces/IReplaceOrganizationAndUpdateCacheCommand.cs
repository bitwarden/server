using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IReplaceOrganizationAndUpdateCacheCommand
{
    /// <summary>
    /// Saves the organization and refreshes its cached abilities.
    /// </summary>
    /// <param name="org">The organization to save.</param>
    /// <param name="orgEvent">When provided, an organization event of this type is logged after the save.</param>
    Task RunAsync(Organization org, EventType? orgEvent = null);
}
