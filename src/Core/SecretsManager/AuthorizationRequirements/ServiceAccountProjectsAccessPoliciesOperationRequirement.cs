using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ServiceAccountProjectsAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{
}

public static class ServiceAccountProjectsAccessPoliciesOperations
{
    public static readonly ServiceAccountProjectsAccessPoliciesOperationRequirement Replace = new() { Name = nameof(Replace) };
}
