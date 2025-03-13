using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserGroups;

public class OrganizationUserGroupOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUserGroupOperations
{
    public static readonly OrganizationUserGroupOperationRequirement ReadAllIds = new() { Name = nameof(ReadAllIds) };
}
