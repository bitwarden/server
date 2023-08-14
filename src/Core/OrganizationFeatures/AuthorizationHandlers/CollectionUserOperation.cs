using Bit.Core.Entities;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionUserOperationRequirement : OperationAuthorizationRequirement
{
    public Collection Collection { get; set; }
}

public static class CollectionUserOperation
{
    public static readonly CollectionUserOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionUserOperationRequirement Delete = new() { Name = nameof(Delete) };

    public static CollectionUserOperationRequirement CreateForNewCollection(Collection collection) => new() { Name = nameof(CreateForNewCollection), Collection = collection };
}
