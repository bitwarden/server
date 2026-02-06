using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public class OrganizationUserUserDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUserUserDetailsOperations
{
    public static OrganizationUserUserDetailsOperationRequirement ReadAll = new() { Name = nameof(ReadAll) };
}
