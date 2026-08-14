using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Collections.ModifyGroupAccess;

public interface IModifyCollectionGroupAccessCommand
{
    /// <summary>
    /// Validates and applies an add/update/remove delta to one or more collections' group access.
    /// </summary>
    Task<CommandResult> ModifyAsync(ModifyCollectionGroupAccessRequest request);
}
