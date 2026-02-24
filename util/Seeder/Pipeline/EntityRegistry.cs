namespace Bit.Seeder.Pipeline;

/// <summary>
/// Persistent cross-step reference store that survives bulk-commit flushes.
/// </summary>
/// <remarks>
/// When <see cref="BulkCommitter"/> commits entities to the database, it clears the entity
/// lists on <see cref="SeederContext"/> (users, groups, ciphers, etc.). The registry preserves
/// the IDs and keys that downstream steps need to reference those already-committed entities
/// — for example, a cipher step needs collection IDs to create join records.
/// <para>
/// Steps populate the registry as they create entities. Later steps read from it.
/// <see cref="RecipeExecutor"/> calls <see cref="Clear"/> before each run to prevent stale state.
/// </para>
/// </remarks>
internal sealed class EntityRegistry
{
    /// <summary>
    /// A user's core IDs and symmetric key, needed for per-user encryption (e.g. personal folders).
    /// </summary>
    internal record UserDigest(Guid UserId, Guid OrgUserId, string SymmetricKey);

    /// <summary>
    /// Organization user IDs for hardened (key-bearing) members. Used by group and collection steps for assignment.
    /// </summary>
    internal List<Guid> HardenedOrgUserIds { get; } = [];

    /// <summary>
    /// Full user references including symmetric keys. Used for per-user encrypted content.
    /// </summary>
    /// <seealso cref="UserDigest"/>
    internal List<UserDigest> UserDigests { get; } = [];

    /// <summary>
    /// Group IDs for collection-group assignment.
    /// </summary>
    internal List<Guid> GroupIds { get; } = [];

    /// <summary>
    /// Collection IDs for cipher-collection assignment.
    /// </summary>
    internal List<Guid> CollectionIds { get; } = [];

    /// <summary>
    /// Cipher IDs for downstream reference.
    /// </summary>
    internal List<Guid> CipherIds { get; } = [];

    /// <summary>
    /// Folder IDs per user, for cipher-to-folder assignment.
    /// </summary>
    internal Dictionary<Guid, List<Guid>> UserFolderIds { get; } = [];

    /// <summary>
    /// Clears all registry lists. Called by <see cref="RecipeExecutor"/> before each pipeline run.
    /// </summary>
    internal void Clear()
    {
        HardenedOrgUserIds.Clear();
        UserDigests.Clear();
        GroupIds.Clear();
        CollectionIds.Clear();
        CipherIds.Clear();
        UserFolderIds.Clear();
    }
}
