using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyUserAccess;

public interface IModifyCollectionUserAccessCommand
{
    /// <summary>
    /// Validates and applies an add/update/remove delta to one or more collections' user access.
    /// </summary>
    Task<CommandResult> ModifyAsync(ModifyCollectionUserAccessRequest request);
}
