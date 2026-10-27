# Access shape

The `accessShape` preset block reproduces a **real** large organization rather than an archetype. It exists because org
load depends on _how_ members reach items, not on item count. Access Intelligence downloads every org item (default
collections and trash included), then maps each item to every member who reaches it through a shared collection,
whatever the member's status. Sync returns one row per item × collection × access path, so overlapping groups multiply
it. The older member-access (DIRT) report follows the same paths, one row per item × collection × path. The `density`
algorithms can't express that shape: each user is in one group, collection sizes are near-uniform, and grants are
round-robin. Reference presets: [`scale/xl-migrated-cyberdyne.json`](../fixtures/presets/scale/xl-migrated-cyberdyne.json)
and [`scale/sm-migrated-cyberdyne.json`](../fixtures/presets/scale/sm-migrated-cyberdyne.json) at 1/10.

| Block                                                      | Step                                                             | What it adds                                                                                           |
| ---------------------------------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `accessShape.groups`                                       | `CreateShapedGroupsStep` (replaces `CreateGroupsStep`)           | Everyone group, hot groups, long-tail sizes, members in up to `maxGroupsPerUser` groups, nested groups |
| `accessShape.collections` / `groupGrants` / `directGrants` | `CreateShapedCollectionsStep` (replaces `CreateCollectionsStep`) | Histogram collection sizes (empties included), hot pairs, weighted group and direct grants             |
| `accessShape.inactiveAccessRate`                           | `CreateShapedCollectionsStep`                                    | Invited, accepted and revoked members who keep group memberships and direct grants                     |
| —                                                          | `GenerateCiphersStep`                                            | Fills each shaped collection to its target size                                                        |

The reference presets also set `users.statusMix`, `users.adminCount`, `policies` and `defaultCollections`; see
[Organization blocks](presets.md#organization-blocks).

The access-shape draws and the default-collection draws all derive from `accessShape.seed`, so a preset reproduces its
shape. `PresetValidator` rejects density settings that `accessShape` would ignore, so a preset uses one model at a time.
It also rejects `roster` with `collections`, because the roster's collections would take the first collection slots.
The shaped loops fail the run if they fall more than 1% short of a target.

## Knobs, and what each one moves

Sizes use histograms: `[{ "min", "max", "weight" }]`. Bucket counts are exact, and values within a bucket are log-uniform.

- **`departments.count` / `locality`.** Members, groups and collections get a home department. Group draws weight
  members so a group's expected in-department share is `locality`. Random group grants, direct grants and
  multi-collection items pick in-department with probability `locality`. This creates path overlap.
- **`groups.hotGroupSizes`, `hotGroupParents`, `hotGroupOverlap`, `groupGrants.hotCollections` / `hotPairs`.**
  Mid-sized groups granted very large collections. In real orgs this pairing dominates report rows. Hot groups can be
  nested in a chain (C inside B inside A): up to `hotGroupOverlap` of each hot group's members come from its parent.
  When they share collections, every common member gets one path per group, and that is where most duplicate sync
  rows come from.
- **`groupGrants.wideGroups`.** A few tiny groups (1 to `maxMembers` members, never the everyone or a hot group)
  granted nested prefixes of one block of `blockSize` near-empty collections (at most `blockMaxItems` items). The first
  wide group gets `maxGrants`; later ones draw from [`minGrants`, `maxGrants`), log-uniform. They set the maximum
  collections a member reaches, and they make part of the block granted by many groups, without adding many items.
- **`groupGrants.total`, `grantedGroupRate`, `groupAppetiteAlpha`, `maxGrantsPerGroup`, `groupSizeBias`.** `total`
  counts every group grant: hot pairs, everyone, wide, random and echo. The rest set which regular groups get random
  grants, how unevenly, and the cap on random grants per group. The groups `grantedGroupRate` leaves out are the
  smallest ones. Together these control median and P90 reach and total group report rows.
- **`collectionPopularityAlpha`, `collectionSizeBias`.** Whether random grants pile onto a few collections, and onto big
  or small ones. Keep popularity mild: a real org's busiest collection is granted by about 20 groups.
- **`groups.nestedGroupRate` + `groupGrants.echoRate`.** Nested child groups are drawn from a larger parent's members.
  When the per-member cap leaves too few of them, the child is topped up by a draw weighted to the parent's
  department, so it is not always a strict subset. Echo grants copy an earlier random grant to a child of its group,
  or else to an overlapping group no bigger than it. Echo grants bypass `maxGrantsPerGroup`, the `grantedGroupRate`
  cutoff and the wide-group exclusion. These are secondary sources of duplicate paths.
- **`directGrants.collectionSizeBias`, `userAffinityAlpha`, `ungroupedBoost`.** The volume and concentration of direct
  report rows. `ungroupedBoost` multiplies the weight of members in no group except the everyone group, so direct
  access goes mostly to members outside team groups.
- **`inactiveAccessRate`.** The share of invited, accepted and revoked members who copy a random confirmed member's
  group memberships (except the everyone group) and direct grants. Real orgs leave revoked members in their groups, and
  Access Intelligence maps them, so this moves member-item pairs but not sync. It runs on its own random stream, so it
  doesn't change the confirmed members' shape.
- **`defaultCollections.deletedRate` with `cipherAssignment.deletedRate` / `maxDeletedCiphers`.** Where the trash sits.
  In a migrated org most trashed items are in default collections. Trash in default collections comes on top of the
  active items, while pool trash takes slots in shared collections, so lowering pool trash raises active shared rows
  unless `totalAssignments` and `hotCollections` come down with it.

What `defaultCollections` writes is in [verification.md](verification.md#q12-default-collections).

## Calibrating against a target org

The `cyberdyne` presets were calibrated this way against one large EU org for DBOPS-257 (snapshots 2026-10-07 and
2026-10-11).

Measure the target org's rows, not just its counts. The load metrics, in order:

1. **Items fetched**: every org cipher, default collections and trash included.
2. **Member-item pairs**: distinct (member, item) through shared collections, members of every status, split by status.
   This is what Access Intelligence maps today. Items in default collections map to no member on that path.
3. **Sync rows per distinct item** and per-member reach (P50/P90/P99/max, and P99 sync rows).

For the structure behind them, also measure groups per member, top groups by member-item pairs, grants per group,
group overlap and nesting, and how many groups grant each collection. Member-access report rows by grant source are a
useful secondary check. Tune the knobs above until the seeded org matches.

## Verify

`density` verification queries check counts. For access shape, check the resulting load instead: items fetched,
member-item pairs by member status, sync rows per distinct item, and per-member collection/item reach
(P50/P90/P99/max). [Q13 in verification.md](verification.md#q13-access-shape-load) covers items fetched and member-item
pairs by status; sync rows and reach need their own queries. Compare the results with the numbers the preset was
calibrated to.
