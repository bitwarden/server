#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ProjectServiceAccountsAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{

}

public static class ProjectServiceAccountsAccessPoliciesOperations
{
    public static readonly ProjectServiceAccountsAccessPoliciesOperationRequirement Updates = new() { Name = nameof(Updates) };
}
