using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationDeleteCommand
{
    /// <summary>
    /// Permanently deletes an organization and performs necessary cleanup.
    /// </summary>
    /// <param name="organization">The organization to delete.</param>
    /// <exception cref="BadRequestException">Thrown when the organization cannot be deleted due to configuration constraints.</exception>
    Task DeleteAsync(Organization organization);
}
