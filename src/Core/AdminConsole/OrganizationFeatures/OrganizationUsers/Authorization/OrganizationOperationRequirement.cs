#nullable enable

using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Authorization;

public abstract class OrganizationOperationRequirement : OperationAuthorizationRequirement
{
    public Guid OrganizationId { get; }

    public OrganizationOperationRequirement(string name, Guid organizationId)
    {
        Name = name;
        OrganizationId = organizationId;
    }
}
