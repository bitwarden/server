using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ProjectServiceAccountsAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{
}

public static class ProjectServiceAccountsAccessPoliciesOperations
{
    public static readonly ProjectServiceAccountsAccessPoliciesOperationRequirement Replace = new() { Name = nameof(Replace) };
}
