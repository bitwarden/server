namespace Bit.Seeder.Models;

/// <summary>
/// Production-calibrated access shape: overlapping group membership, long-tailed collection sizes,
/// and explicit group/user grants. When present, replaces the density group/collection/assignment algorithms.
/// </summary>
internal record SeedPresetAccessShape
{
    /// <summary>
    /// Random seed for the shape. Unset means 1.
    /// </summary>
    public int? Seed { get; init; }

    /// <summary>
    /// Group membership shape. Required when the preset has a <c>groups</c> block.
    /// </summary>
    public SeedPresetShapeGroups? Groups { get; init; }

    /// <summary>
    /// Shared collection sizes. Unset means every collection holds one item.
    /// </summary>
    public SeedPresetShapeCollections? Collections { get; init; }

    /// <summary>
    /// Collection grants to groups: hot pairs, the everyone group, wide groups and weighted random grants.
    /// </summary>
    public SeedPresetShapeGroupGrants? GroupGrants { get; init; }

    /// <summary>
    /// Collection grants straight to members. Unset means no direct grants.
    /// </summary>
    public SeedPresetShapeDirectGrants? DirectGrants { get; init; }

    /// <summary>
    /// Org-structure locality for groups, grants and multi-collection items. Unset means one department, so no locality.
    /// </summary>
    public SeedPresetShapeDepartments? Departments { get; init; }

    /// <summary>
    /// Fraction of invited, accepted and revoked members who copy a random confirmed member's group
    /// memberships (except the everyone group) and direct grants. Real orgs leave revoked members in their groups.
    /// </summary>
    public double? InactiveAccessRate { get; init; }
}

/// <summary>
/// Org-structure locality: members, groups and collections each get a home department, and group membership,
/// grants and multi-collection items stay inside it with probability <see cref="Locality"/>. This is what makes
/// members reach the same collection through several groups (duplicate sync rows), as in real directory-synced orgs.
/// </summary>
internal record SeedPresetShapeDepartments
{
    /// <summary>
    /// Number of departments.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// 0 = no locality (independent random picks), 1 = everything stays in-department.
    /// </summary>
    public double Locality { get; init; }
}

/// <summary>
/// Group membership shape: an optional everyone group, fixed-size hot groups, and histogram-sized regular groups.
/// </summary>
internal record SeedPresetShapeGroups
{
    /// <summary>
    /// Fraction of confirmed members in the single "everyone" group (0 = no everyone group).
    /// </summary>
    public double? EveryoneGroupRate { get; init; }

    /// <summary>
    /// Exact member counts for "hot" groups, each paired with large collections via
    /// <see cref="SeedPresetShapeHotPair"/>.
    /// </summary>
    public List<int>? HotGroupSizes { get; init; }

    /// <summary>
    /// Fraction of each later hot group's members drawn from its parent hot group's members
    /// (overlapping teams granted the same big collections).
    /// </summary>
    public double? HotGroupOverlap { get; init; }

    /// <summary>
    /// Parent hot group index per hot group (-1 = none), for chains like C ⊂ B ⊂ A.
    /// When omitted, every later hot group draws from the first.
    /// </summary>
    public List<int>? HotGroupParents { get; init; }

    /// <summary>
    /// Fraction of regular groups drawn from the members of a larger group in the same department
    /// (nested directory groups). Echo grants prefer these parent→child links.
    /// </summary>
    public double? NestedGroupRate { get; init; }

    /// <summary>
    /// Member-count histogram for the remaining groups.
    /// </summary>
    public List<SeedPresetSizeBucket>? Sizes { get; init; }

    /// <summary>
    /// Cap on groups per member, including the everyone group.
    /// </summary>
    public int? MaxGroupsPerUser { get; init; }

    /// <summary>
    /// Pareto exponent for per-member group affinity; lower = heavier tail.
    /// </summary>
    public double? UserAffinityAlpha { get; init; }
}

/// <summary>
/// Shared collection sizes: a long-tailed item-count histogram, optionally scaled to an exact total.
/// </summary>
internal record SeedPresetShapeCollections
{
    /// <summary>
    /// Item-count histogram for shared collections; a 0..0 bucket makes empty collections.
    /// </summary>
    public List<SeedPresetSizeBucket>? Sizes { get; init; }

    /// <summary>
    /// Total collection-cipher rows across shared collections; histogram draws are scaled to hit it.
    /// </summary>
    public int? TotalAssignments { get; init; }

    /// <summary>
    /// Cap on collections per cipher.
    /// </summary>
    public int? MaxCollectionsPerCipher { get; init; }
}

/// <summary>
/// One hot group and the hot collections granted to it.
/// </summary>
internal record SeedPresetShapeHotPair
{
    /// <summary>
    /// Index into <see cref="SeedPresetShapeGroups.HotGroupSizes"/>.
    /// </summary>
    public int Group { get; init; }

    /// <summary>
    /// Indices into <see cref="SeedPresetShapeGroupGrants.HotCollections"/> granted to that group.
    /// Two hot groups may share collections.
    /// </summary>
    public List<int> Collections { get; init; } = [];
}

/// <summary>
/// Collection grants to groups. Hot pairs, the everyone group and wide groups get their grants first; weighted
/// random grants to the regular groups make up the rest of <see cref="Total"/>.
/// </summary>
internal record SeedPresetShapeGroupGrants
{
    /// <summary>
    /// Total CollectionGroup rows, including hot-pair and everyone-group grants.
    /// </summary>
    public int? Total { get; init; }

    /// <summary>
    /// Fraction of regular groups eligible for random grants, largest first (hot and wide groups get none).
    /// Echo grants can still reach groups outside the cutoff. Real orgs have many directory-synced groups with no grants.
    /// </summary>
    public double? GrantedGroupRate { get; init; }

    /// <summary>
    /// Pareto exponent for per-group grant appetite; lower = a few groups hold most grants while most
    /// granted groups have only a handful. Unset = no appetite skew.
    /// </summary>
    public double? GroupAppetiteAlpha { get; init; }

    /// <summary>
    /// Cap on random grants per group. Hot, wide and echo grants don't count toward it, so a group can end up
    /// above it. Real non-admin groups top out at a few hundred collections.
    /// </summary>
    public int? MaxGrantsPerGroup { get; init; }

    /// <summary>
    /// Group pick weight = members^-bias (× appetite). Higher favors small groups.
    /// </summary>
    public double? GroupSizeBias { get; init; }

    /// <summary>
    /// Pareto exponent for per-collection grant popularity; lower = a few collections granted to many
    /// groups (department-wide collections), which is what gives members several paths to the same item.
    /// Unset = uniform.
    /// </summary>
    public double? CollectionPopularityAlpha { get; init; }

    /// <summary>
    /// Collection pick weight multiplier = (items + 1)^bias. Negative favors small collections (wide grants
    /// that reach many collections but few items).
    /// </summary>
    public double? CollectionSizeBias { get; init; }

    /// <summary>
    /// Fraction of random grants that copy an existing grant to a nested child of its group, or else to a group
    /// sharing members that is no bigger, giving shared members a second path. Echo grants ignore
    /// <see cref="MaxGrantsPerGroup"/> and the <see cref="GrantedGroupRate"/> cutoff, so they can land on any such
    /// group, including hot and wide groups.
    /// </summary>
    public double? EchoRate { get; init; }

    /// <summary>
    /// Number of small non-empty collections granted to the everyone group.
    /// </summary>
    public int? EveryoneGroupCollections { get; init; }

    /// <summary>
    /// Exact item counts of the large collections reserved for hot pairs.
    /// </summary>
    public List<int>? HotCollections { get; init; }

    /// <summary>
    /// Hot groups and the hot collections granted to each.
    /// </summary>
    public List<SeedPresetShapeHotPair>? HotPairs { get; init; }

    /// <summary>
    /// Tiny groups granted a large shared block of near-empty collections. Unset means none.
    /// </summary>
    public SeedPresetShapeWideGroups? WideGroups { get; init; }
}

/// <summary>
/// A few tiny groups granted a large shared block of mostly empty collections (admin-style "see everything"
/// groups). They give a handful of members very wide collection reach with few items, and make those collections
/// granted by many groups.
/// </summary>
internal record SeedPresetShapeWideGroups
{
    /// <summary>
    /// Number of wide groups.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Collections in the shared block.
    /// </summary>
    public required int BlockSize { get; init; }

    /// <summary>
    /// Only collections with at most this many items go into the block.
    /// </summary>
    public required int BlockMaxItems { get; init; }

    /// <summary>
    /// Wide groups after the first draw their grant count log-uniformly from [MinGrants, MaxGrants), floored and
    /// capped at BlockSize. When MinGrants equals MaxGrants, they all get exactly that many.
    /// </summary>
    public required int MinGrants { get; init; }

    /// <summary>
    /// Grant count of the first wide group (capped at BlockSize). Later wide groups stay below it unless it equals
    /// MinGrants.
    /// </summary>
    public required int MaxGrants { get; init; }

    /// <summary>
    /// Wide groups are picked from regular groups with at most this many members.
    /// </summary>
    public int MaxMembers { get; init; } = 3;
}

/// <summary>
/// Collection grants straight to confirmed members, weighted toward big collections and heavy-tailed members.
/// </summary>
internal record SeedPresetShapeDirectGrants
{
    /// <summary>
    /// Total CollectionUser rows on shared collections.
    /// </summary>
    public int? Total { get; init; }

    /// <summary>
    /// Collection pick weight = (items + 1)^bias. Higher favors big collections.
    /// </summary>
    public double? CollectionSizeBias { get; init; }

    /// <summary>
    /// Pareto exponent for per-member direct-grant affinity; lower = heavier tail.
    /// </summary>
    public double? UserAffinityAlpha { get; init; }

    /// <summary>
    /// Weight multiplier for members in no group other than the everyone group. Real orgs give direct
    /// access mainly to people outside the team groups.
    /// </summary>
    public double? UngroupedBoost { get; init; }
}
