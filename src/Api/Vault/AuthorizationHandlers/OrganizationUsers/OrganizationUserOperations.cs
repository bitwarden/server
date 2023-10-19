using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.OrganizationUsers;

public class OrganizationUserOperationRequirement : OperationAuthorizationRequirement
{
    public Guid OrganizationId { get; }

    public OrganizationUserOperationRequirement(string name, Guid organizationId)
    {
        Name = name;
        OrganizationId = organizationId;
    }
}

public static class OrganizationUserOperations
{
    public static OrganizationUserOperationRequirement Read(Guid organizationId)
    {
        return new OrganizationUserOperationRequirement(nameof(Read), organizationId);
    }
}
