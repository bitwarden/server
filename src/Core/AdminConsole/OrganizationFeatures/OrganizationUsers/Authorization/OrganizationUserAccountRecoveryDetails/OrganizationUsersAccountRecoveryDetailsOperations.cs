using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUserAccountRecoveryDetails;

public class OrganizationUsersAccountRecoveryDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUsersAccountRecoveryDetailsOperations
{
    public static readonly OrganizationUsersAccountRecoveryDetailsOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly OrganizationUsersAccountRecoveryDetailsOperationRequirement ReadAll = new() { Name = nameof(ReadAll) };
}
