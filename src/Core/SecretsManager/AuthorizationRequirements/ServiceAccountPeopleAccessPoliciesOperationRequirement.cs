using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ServiceAccountPeopleAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{
}

public static class ServiceAccountPeopleAccessPoliciesOperations
{
    public static readonly ServiceAccountPeopleAccessPoliciesOperationRequirement Replace = new() { Name = nameof(Replace) };
}
