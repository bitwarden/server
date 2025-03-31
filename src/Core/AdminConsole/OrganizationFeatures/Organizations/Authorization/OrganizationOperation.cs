using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Authorization;

public class OrganizationOperationRequirement : OperationAuthorizationRequirement;

public static class OrganizationOperations
{
    public static OrganizationOperationRequirement Update = new() { Name = nameof(Update) };
}
