using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationCollectionEnhancements.Interfaces;

/// <summary>
/// Enable collection enhancements for an organization.
/// This command will be deprecated once all organizations have collection enhancements enabled.
/// </summary>
public interface IOrganizationEnableCollectionEnhancementsCommand
{
    Task EnableCollectionEnhancements(Organization organization);
}
