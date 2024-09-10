using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;

public class OrganizationUserUserDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUserUserDetailsOperations
{
    public static OrganizationUserUserDetailsOperationRequirement ReadAll = new() { Name = nameof(ReadAll) };
}
