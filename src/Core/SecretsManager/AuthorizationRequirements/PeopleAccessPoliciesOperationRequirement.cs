using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class PeopleAccessPoliciesOperationRequirement : OperationAuthorizationRequirement
{
}

public static class PeopleAccessPoliciesOperations
{
    public static readonly PeopleAccessPoliciesOperationRequirement ReplaceProjectPeople = new() { Name = nameof(ReplaceProjectPeople) };
}
