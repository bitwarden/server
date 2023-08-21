using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionGroupOperationRequirement : OperationAuthorizationRequirement { }

public static class CollectionGroupOperation
{
    public static readonly CollectionGroupOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionGroupOperationRequirement Delete = new() { Name = nameof(Delete) };
}
