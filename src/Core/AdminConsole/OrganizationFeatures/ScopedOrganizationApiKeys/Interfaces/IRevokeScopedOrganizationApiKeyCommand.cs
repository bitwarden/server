using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.ScopedOrganizationApiKeys.Interfaces;

public interface IRevokeScopedOrganizationApiKeyCommand
{
    Task<CommandResult> RevokeAsync(Guid organizationId, Guid id);
}
