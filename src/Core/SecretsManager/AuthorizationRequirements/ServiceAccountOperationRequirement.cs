using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ServiceAccountOperationRequirement : OperationAuthorizationRequirement
{
}

public static class ServiceAccountOperations
{
    public static readonly ServiceAccountOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly ServiceAccountOperationRequirement Read = new() { Name = nameof(Read) };
    public static readonly ServiceAccountOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly ServiceAccountOperationRequirement ReadAccessTokens = new() { Name = nameof(ReadAccessTokens) };
    public static readonly ServiceAccountOperationRequirement CreateAccessToken = new() { Name = nameof(CreateAccessToken) };
    public static readonly ServiceAccountOperationRequirement RevokeAccessTokens = new() { Name = nameof(RevokeAccessTokens) };
}
