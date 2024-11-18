using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Bit.Core.Tools.Authorization;

public class VaultExportOperationRequirement : OperationAuthorizationRequirement;

public static class VaultExportOperations
{
    /// <summary>
    /// Exporting the entire organization vault.
    /// </summary>
    public static readonly VaultExportOperationRequirement ExportAll =
        new() { Name = nameof(ExportAll) };

    /// <summary>
    /// Exporting only the organization items that the user has Can Manage permissions for
    /// </summary>
    public static readonly VaultExportOperationRequirement ExportPartial =
        new() { Name = nameof(ExportPartial) };
}
