namespace Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;

public interface ISameOrganizationQuery
{
    Task<bool> OrgUsersInTheSameOrgAsync(List<Guid> organizationUserIds, Guid organizationId);
    Task<bool> GroupsInTheSameOrgAsync(List<Guid> groupIds, Guid organizationId);
}
