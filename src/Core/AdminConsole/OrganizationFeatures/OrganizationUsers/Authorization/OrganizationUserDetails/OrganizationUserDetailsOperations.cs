using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserDetails;

public class OrganizationUserDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUserDetailsOperations
{
    public static readonly OrganizationUserDetailsOperationRequirement Read = new() { Name = nameof(Read) };
}
