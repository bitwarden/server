using Bit.Core.AdminConsole.Models.Data;

namespace Bit.Api.AdminConsole.Authorization;

public interface IGetActingUserForOrganizationQuery
{
    /// <summary>
    /// Resolves the caller into the <see cref="IActingUser"/> that represents their role in a given organization:
    /// a <see cref="StandardUser"/> when they are a member (read from the current context), or a
    /// <see cref="ProviderUser"/> when they manage the organization through a linked provider.
    /// </summary>
    Task<IActingUser> GetActingUserAsync(Guid userId, Guid organizationId);
}
