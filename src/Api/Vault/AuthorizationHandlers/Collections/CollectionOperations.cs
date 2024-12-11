using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public class CollectionOperationRequirement : OperationAuthorizationRequirement
{
    public Guid OrganizationId { get; init; }

    public CollectionOperationRequirement(string name, Guid organizationId)
    {
        Name = name;
        OrganizationId = organizationId;
    }
}

public static class CollectionOperations
{
    public static CollectionOperationRequirement ReadAll(Guid organizationId)
    {
        return new CollectionOperationRequirement(nameof(ReadAll), organizationId);
    }

    public static CollectionOperationRequirement ReadAllWithAccess(Guid organizationId)
    {
        return new CollectionOperationRequirement(nameof(ReadAllWithAccess), organizationId);
    }
}
