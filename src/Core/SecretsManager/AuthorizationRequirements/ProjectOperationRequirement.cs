using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ProjectOperationRequirement : OperationAuthorizationRequirement { }

public static class ProjectOperations
{
    public static readonly ProjectOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly ProjectOperationRequirement Update = new() { Name = nameof(Update) };
    public static readonly ProjectOperationRequirement Delete = new() { Name = nameof(Delete) };
}
