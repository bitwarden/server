using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys;

public class RevokeOrganizationScopedApiKeyCommand(
    IOrganizationScopedApiKeyRepository organizationScopedApiKeyRepository,
    IOrganizationRepository organizationRepository,
    IEventService eventService)
    : IRevokeOrganizationScopedApiKeyCommand
{
    public async Task<CommandResult> RevokeAsync(Guid organizationId, Guid id)
    {
        var apiKey = await organizationScopedApiKeyRepository.GetByIdAsync(id);
        if (apiKey is null || apiKey.OrganizationId != organizationId)
        {
            return new ScopedApiKeyNotFound();
        }

        await organizationScopedApiKeyRepository.DeleteAsync(apiKey);

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is not null)
        {
            await eventService.LogOrganizationEventAsync(organization, EventType.Organization_ScopedApiKeyRevoked);
        }

        return new None();
    }
}
