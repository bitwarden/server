namespace Bit.Core.Queries.Interfaces;

public interface IOrganizationHasConfirmedOwnersExceptQuery
{
    Task<bool> HasConfirmedOwnersExceptAsync(Guid organizationId, IEnumerable<Guid> organizationUsersId,
        bool includeProvider = true);
}
