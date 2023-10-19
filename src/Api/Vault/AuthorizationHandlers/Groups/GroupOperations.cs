using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Vault.AuthorizationHandlers.Groups;

public class GroupOperationRequirement : OperationAuthorizationRequirement { }

public static class GroupOperations
{
    public static readonly GroupOperationRequirement Read = new() { Name = nameof(Read) };
}
