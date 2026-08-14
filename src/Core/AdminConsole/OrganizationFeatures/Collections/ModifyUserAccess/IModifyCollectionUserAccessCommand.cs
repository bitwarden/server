using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

/// <summary>
/// Applies an add/update/remove delta to one or more collections' user access.
/// </summary>
public interface IModifyCollectionUserAccessCommand
{
    /// <summary>
    /// Validates and persists the delta.
    /// </summary>
    Task<CommandResult> ModifyAsync(ModifyCollectionUserAccessRequest request);
}
