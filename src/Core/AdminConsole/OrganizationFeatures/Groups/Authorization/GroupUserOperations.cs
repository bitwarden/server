using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

public class GroupUserOperationRequirement : OperationAuthorizationRequirement
{
    public GroupUserOperationRequirement(string name)
    {
        Name = name;
    }
}

public static class GroupUserOperations
{
    public static readonly GroupUserOperationRequirement AssignUsers = new(nameof(AssignUsers));
}
