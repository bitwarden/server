using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class GroupUserOperationRequirement : OperationAuthorizationRequirement { }

public static class GroupUserOperations
{
    public static readonly GroupUserOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly GroupUserOperationRequirement Delete = new() { Name = nameof(Delete) };
}
