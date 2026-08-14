#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// The outcome of an <see cref="ICollectionAuthorizationService"/> check.
/// </summary>
public record CollectionAuthorizationResult(bool CanUpdateCollection, bool CanModifyUserAccess, bool CanModifyGroupAccess)
{
    public bool IsSuccess => CanUpdateCollection && CanModifyUserAccess && CanModifyGroupAccess;
}
