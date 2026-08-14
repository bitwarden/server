using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

/// <summary>
/// Applies an add/update/remove delta to one or more collections' group access.
/// </summary>
public interface IModifyCollectionGroupAccessCommand
{
    /// <summary>
    /// Validates and persists the delta.
    /// </summary>
    Task<CommandResult> ModifyAsync(ModifyCollectionGroupAccessRequest request);
}
