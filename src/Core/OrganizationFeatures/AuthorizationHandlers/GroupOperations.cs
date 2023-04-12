using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class GroupOperationRequirement : OperationAuthorizationRequirement { }

public static class GroupOperations
{
    // Operations on the Group object itself
    public static readonly GroupOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly GroupOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly GroupOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly GroupOperationRequirement Delete = new() { Name = nameof(Delete) };

    // Operations on Group-User associations 
    public static readonly GroupOperationRequirement AddUser = new() { Name = nameof(AddUser) };
    public static readonly GroupOperationRequirement DeleteUser = new() { Name = nameof(DeleteUser) };
}
