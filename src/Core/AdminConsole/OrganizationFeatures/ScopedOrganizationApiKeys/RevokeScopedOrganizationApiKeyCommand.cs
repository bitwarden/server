using Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using OneOf.Types;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys;

public class RevokeScopedOrganizationApiKeyCommand(
    IApiKeyRepository apiKeyRepository,
    IOrganizationRepository organizationRepository,
    IEventService eventService)
    : IRevokeScopedOrganizationApiKeyCommand
{
    public async Task<CommandResult> RevokeAsync(Guid organizationId, Guid id)
    {
        var apiKey = await apiKeyRepository.GetByIdAsync(id);
        if (apiKey is null || apiKey.OrganizationId != organizationId)
        {
            return new ScopedApiKeyNotFound();
        }

        await apiKeyRepository.DeleteAsync(apiKey);

        var organization = await organizationRepository.GetByIdAsync(organizationId);
        if (organization is not null)
        {
            await eventService.LogOrganizationEventAsync(organization, EventType.Organization_ScopedApiKeyRevoked);
        }

        return new None();
    }
}
