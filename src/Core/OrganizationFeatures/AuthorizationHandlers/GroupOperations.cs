using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class GroupOperationRequirement : OperationAuthorizationRequirement { }

public static class GroupOperations
{
    // Operations on the Group object itself
    public static readonly GroupOperationRequirement Create = new() { Name = "Create" };
    public static readonly GroupOperationRequirement Read = new() { Name = "Read" };
    public static readonly GroupOperationRequirement Update = new() { Name = "Update" };
    public static readonly GroupOperationRequirement Delete = new() { Name = "Delete" };

    // Operations on Group-User associations 
    public static readonly GroupOperationRequirement AddUser = new() { Name = "AddUser" };
    public static readonly GroupOperationRequirement DeleteUser = new() { Name = "DeleteUser" };
}
