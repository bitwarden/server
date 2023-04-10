using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class OrganizationOperationRequirement : OperationAuthorizationRequirement { }

public static class OrganizationOperations
{
    public static readonly OrganizationOperationRequirement ReadAllGroupsRequirement = new() { Name = "ReadAllGroups" };
}

static class OrganizationAccessPolicies
{
    public static AuthorizationPolicy ReadAllGroups = new AuthorizationPolicyBuilder()
        .AddRequirements(OrganizationOperations.ReadAllGroupsRequirement)
        .Build();
}
