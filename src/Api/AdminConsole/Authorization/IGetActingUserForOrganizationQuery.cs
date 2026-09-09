using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Api.AdminConsole.Authorization;

public interface IGetActingUserForOrganizationQuery
{
    /// <summary>
    /// Resolves the caller into the <see cref="StandardUser"/> that represents their role in a given organization.
    /// When they are a member, their role and permissions are read from the current context. When they manage the
    /// organization through a linked provider, a <see cref="StandardUser"/> flagged as a provider is returned.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// Thrown when the caller is neither a member of the organization nor manages it through a linked provider.
    /// </exception>
    Task<IActingUser> GetActingUserAsync(Guid userId, Guid organizationId);
}
