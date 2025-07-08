﻿using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.SecretsManager.AuthorizationRequirements;

#nullable enable

public class BulkSecretOperationRequirement : OperationAuthorizationRequirement
{
}

public static class BulkSecretOperations
{
    public static readonly BulkSecretOperationRequirement ReadAll = new() { Name = nameof(ReadAll) };
}
