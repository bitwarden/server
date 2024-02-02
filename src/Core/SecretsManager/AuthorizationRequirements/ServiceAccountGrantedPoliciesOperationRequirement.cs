using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ServiceAccountGrantedPoliciesOperationRequirement : OperationAuthorizationRequirement
{

}

public static class ServiceAccountGrantedPoliciesOperations
{
    public static readonly ServiceAccountGrantedPoliciesOperationRequirement Replace = new() { Name = nameof(Replace) };
}
