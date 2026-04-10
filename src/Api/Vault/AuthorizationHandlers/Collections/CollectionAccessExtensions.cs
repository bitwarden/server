using Bit.Api.Models.Request;
using Bit.Core.Models.Data;

namespace Bit.Api.Vault.AuthorizationHandlers.Collections;

public static class CollectionAccessExtensions
{
    /// <summary>
    /// Compares posted collection access against current access and returns
    /// the ids categorized as creates, updates, or deletes.
    /// </summary>
    public static (HashSet<Guid> CreateIds, HashSet<Guid> UpdateIds, HashSet<Guid> DeleteIds)
        DiffCollectionAccess(
            this IEnumerable<SelectionReadOnlyRequestModel> posted,
            IEnumerable<CollectionAccessSelection> current)
    {
        var currentIds = current.Select(c => c.Id).ToHashSet();
        var postedIds = posted.Select(p => p.Id).ToHashSet();

        var createIds = postedIds.Where(id => !currentIds.Contains(id)).ToHashSet();
        var updateIds = postedIds.Where(id => currentIds.Contains(id)).ToHashSet();
        var deleteIds = currentIds.Where(id => !postedIds.Contains(id)).ToHashSet();

        return (createIds, updateIds, deleteIds);
    }
}
