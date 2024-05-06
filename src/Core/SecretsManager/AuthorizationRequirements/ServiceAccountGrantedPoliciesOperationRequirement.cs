#nullable enable
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

public class ServiceAccountGrantedPoliciesOperationRequirement : OperationAuthorizationRequirement
{

}

public static class ServiceAccountGrantedPoliciesOperations
{
    public static readonly ServiceAccountGrantedPoliciesOperationRequirement Updates = new() { Name = nameof(Updates) };
}
