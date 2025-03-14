using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization.OrganizationUsersResetPasswordDetails;

public class OrganizationUsersResetPasswordDetailsOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationUsersResetPasswordDetailsOperations
{
    public static readonly OrganizationUsersResetPasswordDetailsOperationRequirement Read = new() { Name = nameof(Read) };
}
