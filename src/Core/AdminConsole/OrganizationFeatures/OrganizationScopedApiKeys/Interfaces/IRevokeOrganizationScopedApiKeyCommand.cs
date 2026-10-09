using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationScopedApiKeys.Interfaces;

public interface IRevokeOrganizationScopedApiKeyCommand
{
    Task<CommandResult> RevokeAsync(Guid organizationId, Guid id);
}
