namespace Bit.Seeder.Models;

/// <summary>
/// One histogram bucket: values in [Min, Max] make up <see cref="Weight"/> of the population.
/// Weights across a bucket list are normalized, so they do not have to sum to 1.0.
/// </summary>
internal record SeedPresetSizeBucket
{
    public int Min { get; init; }

    public int Max { get; init; }

    public double Weight { get; init; }
}

/// <summary>
/// Production-calibrated access shape: overlapping group membership, long-tailed collection sizes,
/// and explicit group/user grants. When present, replaces the density group/collection/assignment algorithms.
/// </summary>
internal record SeedPresetAccessShape
{
    public int? Seed { get; init; }

    public SeedPresetShapeGroups? Groups { get; init; }

    public SeedPresetShapeCollections? Collections { get; init; }

    public SeedPresetShapeGroupGrants? GroupGrants { get; init; }

    public SeedPresetShapeDirectGrants? DirectGrants { get; init; }

    public SeedPresetShapeDepartments? Departments { get; init; }

    /// <summary>Fraction of invited, accepted and revoked members who copy a random confirmed member's group
    /// memberships (except the everyone group) and direct grants. Real orgs leave revoked members in their groups.</summary>
    public double? InactiveAccessRate { get; init; }
}

/// <summary>
/// Org-structure locality: members, groups and collections each get a home department, and group membership,
/// grants and multi-collection items stay inside it with probability <see cref="Locality"/>. This is what makes
/// members reach the same collection through several groups (duplicate sync rows), as in real directory-synced orgs.
/// </summary>
internal record SeedPresetShapeDepartments
{
    public int Count { get; init; } = 1;

    /// <summary>0 = no locality (independent random picks), 1 = everything stays in-department.</summary>
    public double Locality { get; init; }
}

internal record SeedPresetShapeGroups
{
    /// <summary>Fraction of confirmed members in the single "everyone" group (0 = no everyone group).</summary>
    public double? EveryoneGroupRate { get; init; }

    /// <summary>Exact member counts for "hot" groups, each paired with large collections via <see cref="SeedPresetShapeHotPair"/>.</summary>
    public List<int>? HotGroupSizes { get; init; }

    /// <summary>Fraction of each later hot group's members drawn from its parent hot group's members
    /// (overlapping teams granted the same big collections).</summary>
    public double? HotGroupOverlap { get; init; }

    /// <summary>Parent hot group index per hot group (-1 = none), for chains like C ⊂ B ⊂ A.
    /// When omitted, every later hot group draws from the first.</summary>
    public List<int>? HotGroupParents { get; init; }

    /// <summary>Fraction of regular groups built as a subset of a larger group in the same department
    /// (nested directory groups). Echo grants prefer these parent→child links.</summary>
    public double? NestedGroupRate { get; init; }

    /// <summary>Member-count histogram for the remaining groups.</summary>
    public List<SeedPresetSizeBucket>? Sizes { get; init; }

    /// <summary>Cap on groups per member, including the everyone group.</summary>
    public int? MaxGroupsPerUser { get; init; }

    /// <summary>Pareto exponent for per-member group affinity; lower = heavier tail.</summary>
    public double? UserAffinityAlpha { get; init; }
}

internal record SeedPresetShapeCollections
{
    /// <summary>Item-count histogram for shared collections; a 0..0 bucket makes empty collections.</summary>
    public List<SeedPresetSizeBucket>? Sizes { get; init; }

    /// <summary>Total collection-cipher rows across shared collections; histogram draws are scaled to hit it.</summary>
    public int? TotalAssignments { get; init; }

    /// <summary>Cap on collections per cipher.</summary>
    public int? MaxCollectionsPerCipher { get; init; }
}

internal record SeedPresetShapeHotPair
{
    /// <summary>Index into <see cref="SeedPresetShapeGroups.HotGroupSizes"/>.</summary>
    public int Group { get; init; }

    /// <summary>Indices into <see cref="SeedPresetShapeGroupGrants.HotCollections"/> granted to that group.
    /// Two hot groups may share collections.</summary>
    public List<int> Collections { get; init; } = [];
}

internal record SeedPresetShapeGroupGrants
{
    /// <summary>Total CollectionGroup rows, including hot-pair and everyone-group grants.</summary>
    public int? Total { get; init; }

    /// <summary>Fraction of regular groups eligible for random grants, largest first (hot and wide groups only get their own). Real orgs have
    /// many directory-synced groups with no grants.</summary>
    public double? GrantedGroupRate { get; init; }

    /// <summary>Pareto exponent for per-group grant appetite; lower = a few groups hold most grants while most
    /// granted groups have only a handful. Unset = no appetite skew.</summary>
    public double? GroupAppetiteAlpha { get; init; }

    /// <summary>Cap on random grants per group (hot and wide grants excluded). Real non-admin groups top out at a
    /// few hundred collections.</summary>
    public int? MaxGrantsPerGroup { get; init; }

    /// <summary>Group pick weight = members^-bias (× appetite). Higher favors small groups.</summary>
    public double? GroupSizeBias { get; init; }

    /// <summary>Pareto exponent for per-collection grant popularity; lower = a few collections granted to many
    /// groups (department-wide collections), which is what gives members several paths to the same item.
    /// Unset = uniform.</summary>
    public double? CollectionPopularityAlpha { get; init; }

    /// <summary>Collection pick weight multiplier = (items + 1)^bias. Negative favors small collections (wide grants
    /// that reach many collections but few items).</summary>
    public double? CollectionSizeBias { get; init; }

    /// <summary>Fraction of random grants that copy an existing grant to another group sharing members with the
    /// original (nested directory groups granted the same collections), giving shared members a second path.</summary>
    public double? EchoRate { get; init; }

    /// <summary>Number of small non-empty collections granted to the everyone group.</summary>
    public int? EveryoneGroupCollections { get; init; }

    /// <summary>Exact item counts of the large collections reserved for hot pairs.</summary>
    public List<int>? HotCollections { get; init; }

    public List<SeedPresetShapeHotPair>? HotPairs { get; init; }

    public SeedPresetShapeWideGroups? WideGroups { get; init; }
}

/// <summary>
/// A few tiny groups granted a large shared block of mostly empty collections (admin-style "see everything"
/// groups). They give a handful of members very wide collection reach with few items, and make those collections
/// granted by many groups.
/// </summary>
internal record SeedPresetShapeWideGroups
{
    public int Count { get; init; }

    /// <summary>Collections in the shared block.</summary>
    public int BlockSize { get; init; }

    /// <summary>Only collections with at most this many items go into the block.</summary>
    public int BlockMaxItems { get; init; }

    /// <summary>Grants per wide group are drawn log-uniformly from [MinGrants, MaxGrants] (capped at BlockSize).</summary>
    public int MinGrants { get; init; }

    public int MaxGrants { get; init; }

    /// <summary>Wide groups are picked from regular groups with at most this many members.</summary>
    public int MaxMembers { get; init; } = 3;
}

internal record SeedPresetShapeDirectGrants
{
    /// <summary>Total CollectionUser rows on shared collections.</summary>
    public int? Total { get; init; }

    /// <summary>Collection pick weight = (items + 1)^bias. Higher favors big collections.</summary>
    public double? CollectionSizeBias { get; init; }

    /// <summary>Pareto exponent for per-member direct-grant affinity; lower = heavier tail.</summary>
    public double? UserAffinityAlpha { get; init; }

    /// <summary>Weight multiplier for members in no group other than the everyone group. Real orgs give direct
    /// access mainly to people outside the team groups.</summary>
    public double? UngroupedBoost { get; init; }
}

/// <summary>
/// My Items (default user collections) as created by the Organization Data Ownership policy,
/// plus already-migrated org ciphers inside them.
/// </summary>
internal record SeedPresetMyItems
{
    /// <summary>Plaintext collection name; encrypted once with the org key and shared by every My Items collection, as the client does.</summary>
    public string? Name { get; init; }

    /// <summary>Fraction of My Items collections holding at least one item.</summary>
    public double? NonEmptyRate { get; init; }

    /// <summary>Item-count histogram for non-empty My Items collections.</summary>
    public List<SeedPresetSizeBucket>? Sizes { get; init; }

    /// <summary>Exact item counts for the heaviest members (the long tail).</summary>
    public List<int>? Largest { get; init; }

    /// <summary>Trashed items added per member, as a fraction of that member's active My Items items.</summary>
    public double? DeletedRate { get; init; }

    /// <summary>Days before now that the policy was enabled; collections are created then, items migrate after.</summary>
    public int? PolicyEnabledDaysAgo { get; init; }
}

internal record SeedPresetPolicies
{
    /// <summary>Not supported. Kept so <see cref="Pipeline.PresetValidator"/> can reject it rather than silently ignore it.</summary>
    public bool? EnableAll { get; init; }

    /// <summary>Not supported; see <see cref="EnableAll"/>.</summary>
    public List<string>? Except { get; init; }

    /// <summary>PolicyType names to create as enabled policies.</summary>
    public List<string>? Enable { get; init; }

    /// <summary>PolicyType names to create as disabled policy rows.</summary>
    public List<string>? Disable { get; init; }

    /// <summary>Policy Data JSON per PolicyType name; overrides the built-in default.</summary>
    public Dictionary<string, System.Text.Json.JsonElement>? Data { get; init; }
}

internal record SeedPresetStatusMix
{
    public double Confirmed { get; init; }

    public double Invited { get; init; }

    public double Accepted { get; init; }

    public double Revoked { get; init; }
}
