using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class AccessPolicyOperationRequirement : OperationAuthorizationRequirement
{
}

public static class AccessPolicyOperations
{
    public static readonly AccessPolicyOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly AccessPolicyOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly AccessPolicyOperationRequirement Delete = new() { Name = nameof(Delete) };
}
