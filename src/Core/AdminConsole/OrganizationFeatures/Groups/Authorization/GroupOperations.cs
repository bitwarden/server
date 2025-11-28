using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

public class GroupOperationRequirement : OperationAuthorizationRequirement
{
    public GroupOperationRequirement(string name)
    {
        Name = name;
    }
}

public static class GroupOperations
{
    public static readonly GroupOperationRequirement ReadAll = new(nameof(ReadAll));
    public static readonly GroupOperationRequirement ReadAllDetails = new(nameof(ReadAllDetails));
}
