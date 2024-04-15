#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ProjectServiceAccountsPoliciesOperationRequirement : OperationAuthorizationRequirement
{

}

public static class ProjectServiceAccountsPoliciesOperations
{
    public static readonly ProjectServiceAccountsPoliciesOperationRequirement Updates = new() { Name = nameof(Updates) };
}
