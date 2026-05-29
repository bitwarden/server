using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.KeyManagement.Authorization;

public class KeyConnectorOperationsRequirement : OperationAuthorizationRequirement
{
    public KeyConnectorOperationsRequirement(string name)
    {
        Name = name;
    }
}

public static class KeyConnectorOperations
{
    public static readonly KeyConnectorOperationsRequirement Use = new(nameof(Use));
}
