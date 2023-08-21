using Bit.Core.Entities;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionUserOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionUserOperation
{
    public static readonly CollectionUserOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionUserOperationRequirement Delete = new() { Name = nameof(Delete) };
}
