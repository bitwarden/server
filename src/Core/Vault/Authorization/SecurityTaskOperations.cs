using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Vault.Authorization;

public class SecurityTaskOperationRequirement : OperationAuthorizationRequirement
{
    public SecurityTaskOperationRequirement(string name)
    {
        Name = name;
    }
}

public static class SecurityTaskOperations
{
    public static readonly SecurityTaskOperationRequirement Update = new(nameof(Update));
}
