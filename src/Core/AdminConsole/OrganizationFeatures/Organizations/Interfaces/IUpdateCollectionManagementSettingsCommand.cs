using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;

public interface IUpdateCollectionManagementSettingsCommand
{
    /// <summary>
    /// Updates an organization's collection management settings.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to update.</param>
    /// <param name="settings">The collection management settings to apply.</param>
    /// <returns>The updated organization.</returns>
    Task<Organization> UpdateCollectionManagementSettingsAsync(Guid organizationId, OrganizationCollectionManagementSettings settings);
}
