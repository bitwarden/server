using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class SecretAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{
}

public static class SecretAccessPoliciesOperations
{
    public static readonly SecretAccessPoliciesOperationRequirement Updates = new() { Name = nameof(Updates) };
    public static readonly SecretAccessPoliciesOperationRequirement Create = new() { Name = nameof(Create) };
}
