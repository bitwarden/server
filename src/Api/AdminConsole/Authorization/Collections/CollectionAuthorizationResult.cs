#nullable enable

namespace Bit.Api.AdminConsole.Authorization.Collections;

/// <summary>
/// The outcome of an <see cref="ICollectionAuthorizationService"/> check. Kept as three separate flags,
/// rather than a single bool, since each check is a distinct permission set a future caller may want to
/// report on individually - see <see cref="CollectionRules"/> for what each one means.
/// </summary>
public record CollectionAuthorizationResult(bool CanUpdateCollection, bool CanModifyUserAccess, bool CanModifyGroupAccess)
{
    public bool IsSuccess => CanUpdateCollection && CanModifyUserAccess && CanModifyGroupAccess;
}
