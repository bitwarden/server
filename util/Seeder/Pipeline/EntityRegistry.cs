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
    /// Organization user IDs for invited, accepted and revoked generated members. The access-shape collection step can give some of them access.
    /// </summary>
    internal List<Guid> InactiveOrgUserIds { get; } = [];

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
    /// Named folder lookup: (emailPrefix, folderName) → folderId.
    /// Populated by <see cref="Steps.CreateRosterStep"/> when roster users declare named folders.
    /// </summary>
    internal Dictionary<string, Dictionary<string, Guid>> UserNamedFolders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Email prefix → userId lookup for roster users. Used by assignment steps to resolve user references.
    /// </summary>
    internal Dictionary<string, Guid> UserEmailPrefixToUserId { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cipher name → cipher ID lookup for fixture ciphers. Used by assignment steps to resolve cipher references.
    /// </summary>
    internal Dictionary<string, Guid> FixtureCipherNameToId { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Collection name → collection ID lookup for roster collections. Used by assignment steps to resolve collection references.
    /// </summary>
    internal Dictionary<string, Guid> FixtureCollectionNameToId { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Member count per group. Populated by the access-shape group step for grant weighting.
    /// </summary>
    internal Dictionary<Guid, int> GroupMemberCounts { get; } = [];

    /// <summary>
    /// The access-shape "everyone" group, if one was created.
    /// </summary>
    internal Guid? EveryoneGroupId { get; set; }

    /// <summary>
    /// Access-shape hot groups, in preset order. Paired with dedicated large collections.
    /// </summary>
    internal List<Guid> HotGroupIds { get; } = [];

    /// <summary>
    /// Target item count per collection, aligned with <see cref="CollectionIds"/>. When non-empty,
    /// <see cref="Steps.GenerateCiphersStep"/> fills collections to these sizes instead of using density skew.
    /// </summary>
    internal List<int> CollectionTargetSizes { get; } = [];

    /// <summary>
    /// Access-shape department per collection, aligned with <see cref="CollectionIds"/>. Empty when departments are off.
    /// </summary>
    internal List<int> CollectionDepartments { get; } = [];

    /// <summary>
    /// Access-shape department per member, aligned with <see cref="HardenedOrgUserIds"/>, and per group.
    /// </summary>
    internal List<int> MemberDepartments { get; } = [];

    internal Dictionary<Guid, int> GroupDepartments { get; } = [];

    /// <summary>
    /// Access-shape nested groups: parent group → child groups whose members are a subset of the parent's.
    /// </summary>
    internal Dictionary<Guid, List<Guid>> GroupChildren { get; } = [];

    /// <summary>
    /// Probability that a shaped pick stays inside the home department.
    /// </summary>
    internal double DepartmentLocality { get; set; }

    /// <summary>
    /// Cap on collections per cipher when filling <see cref="CollectionTargetSizes"/>.
    /// </summary>
    internal int MaxCollectionsPerCipher { get; set; } = 1;

    /// <summary>
    /// Clears all registry lists. Called by <see cref="RecipeExecutor"/> before each pipeline run.
    /// </summary>
    internal void Clear()
    {
        HardenedOrgUserIds.Clear();
        InactiveOrgUserIds.Clear();
        UserDigests.Clear();
        GroupIds.Clear();
        CollectionIds.Clear();
        CipherIds.Clear();
        UserFolderIds.Clear();
        UserNamedFolders.Clear();
        UserEmailPrefixToUserId.Clear();
        FixtureCipherNameToId.Clear();
        FixtureCollectionNameToId.Clear();
        GroupMemberCounts.Clear();
        EveryoneGroupId = null;
        HotGroupIds.Clear();
        CollectionTargetSizes.Clear();
        MaxCollectionsPerCipher = 1;
        CollectionDepartments.Clear();
        MemberDepartments.Clear();
        GroupDepartments.Clear();
        GroupChildren.Clear();
        DepartmentLocality = 0;
    }
}
