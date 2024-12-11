namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IHasConfirmedOwnersExceptQuery
{
    /// <summary>
    /// Checks if an organization has any confirmed owners except for the ones in the <paramref name="organizationUsersId"/> list.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="organizationUsersId">The organization user IDs to exclude.</param>
    /// <param name="includeProvider">Whether to include the provider users in the count.</param>
    Task<bool> HasConfirmedOwnersExceptAsync(
        Guid organizationId,
        IEnumerable<Guid> organizationUsersId,
        bool includeProvider = true
    );
}
