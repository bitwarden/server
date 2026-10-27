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
    /// Org-structure locality for groups. Unset means one department, so no locality.
    /// </summary>
    public SeedPresetShapeDepartments? Departments { get; init; }
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
    /// <c>accessShape.groupGrants.hotPairs</c>.
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
