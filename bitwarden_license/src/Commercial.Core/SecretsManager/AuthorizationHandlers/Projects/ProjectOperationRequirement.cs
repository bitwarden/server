using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Commercial.Core.SecretsManager.AuthorizationHandlers.Projects;

public class ProjectOperationRequirement : OperationAuthorizationRequirement
{
}

public static class ProjectOperations
{
    public static readonly ProjectOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly ProjectOperationRequirement Update = new() { Name = nameof(Update) };
}
