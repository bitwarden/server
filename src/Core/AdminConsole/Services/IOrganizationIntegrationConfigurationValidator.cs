using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Services;

public interface IOrganizationIntegrationConfigurationValidator
{
    /// <summary>
    /// Validates that the configuration is valid for the given integration type. The configuration must
    /// include a Configuration that is valid for the type, valid Filters, and a non-empty Template
    /// to pass validation.
    /// </summary>
    /// <param name="integrationType">The type of integration</param>
    /// <param name="configuration">The OrganizationIntegrationConfiguration to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateConfiguration(IntegrationType integrationType, OrganizationIntegrationConfiguration configuration);
}
