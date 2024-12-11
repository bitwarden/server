using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ProjectPeopleAccessPoliciesOperationRequirement : OperationAuthorizationRequirement { }

public static class ProjectPeopleAccessPoliciesOperations
{
    public static readonly ProjectPeopleAccessPoliciesOperationRequirement Replace = new()
    {
        Name = nameof(Replace),
    };
}
