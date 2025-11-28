using Bit.Core.AdminConsole.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IOrganizationInitiateDeleteCommand
{
    /// <summary>
    /// Initiates a secure deletion process for an organization by requesting confirmation from an organization admin.
    /// </summary>
    /// <param name="organization">The organization to be deleted.</param>
    /// <param name="orgAdminEmail">The email address of the organization admin who will confirm the deletion.</param>
    /// <exception cref="BadRequestException">Thrown when the specified admin email is invalid or lacks sufficient permissions.</exception>
    Task InitiateDeleteAsync(Organization organization, string orgAdminEmail);
}
