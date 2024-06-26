using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Groups;

public class GroupOperationRequirement : OperationAuthorizationRequirement
{
    public Guid OrganizationId { get; init; }

    public GroupOperationRequirement(string name, Guid organizationId)
    {
        Name = name;
        OrganizationId = organizationId;
    }
}

public static class GroupOperations
{
    public static GroupOperationRequirement ReadAll(Guid organizationId)
    {
        return new GroupOperationRequirement(nameof(ReadAll), organizationId);
    }
}
