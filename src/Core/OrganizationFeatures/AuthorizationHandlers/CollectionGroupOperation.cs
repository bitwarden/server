using Bit.Core.Entities;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.OrganizationFeatures.AuthorizationHandlers;

public class CollectionGroupOperationRequirement : OperationAuthorizationRequirement
{
    public Collection Collection { get; set; }
}

public static class CollectionGroupOperation
{
    public static readonly CollectionGroupOperationRequirement Create = new() { Name = nameof(Create) };
    public static readonly CollectionGroupOperationRequirement Delete = new() { Name = nameof(Delete) };

    public static CollectionGroupOperationRequirement CreateForNewCollection(Collection collection) => new() { Name = nameof(CreateForNewCollection), Collection = collection };
}
