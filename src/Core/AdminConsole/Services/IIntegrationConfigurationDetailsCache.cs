#nullable enable

using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services;

public interface IIntegrationConfigurationDetailsCache
{
    List<OrganizationIntegrationConfigurationDetails> GetConfigurationDetails(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);
}
