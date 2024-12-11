using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Api.Tools.Authorization;

public class VaultExportOperationRequirement : OperationAuthorizationRequirement;

public static class VaultExportOperations
{
    /// <summary>
    /// Exporting the entire organization vault.
    /// </summary>
    public static readonly VaultExportOperationRequirement ExportWholeVault = new()
    {
        Name = nameof(ExportWholeVault),
    };

    /// <summary>
    /// Exporting only the organization items that the user has Can Manage permissions for
    /// </summary>
    public static readonly VaultExportOperationRequirement ExportManagedCollections = new()
    {
        Name = nameof(ExportManagedCollections),
    };
}
