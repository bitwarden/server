using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Groups.Authorization;

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

    public static GroupOperationRequirement ReadDetails(Guid organizationId)
    {
        return new GroupOperationRequirement(nameof(ReadDetails), organizationId);
    }
}
