using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;

public interface ICreateScopedOrganizationApiKeyCommand
{
    Task<CommandResult<CreatedScopedOrganizationApiKey>> CreateAsync(CreateScopedOrganizationApiKeyRequest request);
}
