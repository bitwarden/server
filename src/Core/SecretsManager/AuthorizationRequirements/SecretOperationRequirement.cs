using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class SecretOperationRequirement : OperationAuthorizationRequirement { }

public static class SecretOperations
{
    public static readonly SecretOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly SecretOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly SecretOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly SecretOperationRequirement Delete = new() { Name = nameof(Delete) };
    public static readonly SecretOperationRequirement ReadAccessPolicies = new()
    {
        Name = nameof(ReadAccessPolicies),
    };
}
