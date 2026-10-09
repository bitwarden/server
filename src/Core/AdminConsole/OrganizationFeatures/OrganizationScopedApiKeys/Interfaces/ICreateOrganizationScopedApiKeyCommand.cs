using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;

public interface ICreateOrganizationScopedApiKeyCommand
{
    Task<CommandResult<CreatedOrganizationScopedApiKey>> CreateAsync(CreateOrganizationScopedApiKeyRequest request);
}
