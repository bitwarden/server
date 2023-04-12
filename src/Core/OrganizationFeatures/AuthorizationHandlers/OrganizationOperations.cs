using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class OrganizationOperationRequirement : OperationAuthorizationRequirement { }

public static class OrganizationOperations
{
    public static readonly OrganizationOperationRequirement ReadAllGroups = new() { Name = nameof(ReadAllGroups) };
}
