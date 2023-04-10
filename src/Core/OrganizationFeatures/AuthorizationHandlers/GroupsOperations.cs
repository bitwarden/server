using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class GroupOperationRequirement : OperationAuthorizationRequirement { }

public static class GroupsOperations
{
    public static readonly GroupOperationRequirement ReadGroupRequirement = new() { Name = "ReadGroup" };
}

static class GroupAccessPolicies
{
    public static AuthorizationPolicy ReadGroup = new AuthorizationPolicyBuilder()
        .AddRequirements(GroupsOperations.ReadGroupRequirement)
        .Build();
}
