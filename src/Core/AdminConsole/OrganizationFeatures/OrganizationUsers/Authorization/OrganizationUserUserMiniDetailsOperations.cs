using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserMiniDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUserUserMiniDetailsOperations
{
    public static readonly OrganizationUserUserMiniDetailsOperationRequirement ReadAll = new() { Name = nameof(ReadAll) };
}
