using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Tools.Authorization;

public class VaultExportOperationRequirement : OperationAuthorizationRequirement;

public static class VaultExportOperations
{
    /// <summary>
    /// Represents exporting the entire organization vault.
    /// </summary>
    public static readonly VaultExportOperationRequirement ExportWholeVault =
        new() { Name = nameof(ExportWholeVault) };
}
