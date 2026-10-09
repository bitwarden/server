# Access shape, My Items and policies

Three preset blocks for reproducing a **real** large organization rather than an archetype. They exist because the
member-access (DIRT) report and sync return one row per item × collection × access path, so load depends on *how*
members reach items, not on item count. The `density` algorithms can't express that shape: each user is in one group,
collection sizes are near-uniform, and grants are round-robin. Reference preset:
[`scale/xl-migrated-cyberdyne.json`](../fixtures/presets/scale/xl-migrated-cyberdyne.json).

| Block | Step | What it adds |
| --- | --- | --- |
| `users.statusMix` | `CreateUsersStep` | Exact confirmed / invited / accepted / revoked weights |
| `accessShape.groups` | `CreateShapedGroupsStep` (replaces `CreateGroupsStep`) | Everyone group, hot groups, long-tail sizes, members in up to `maxGroupsPerUser` groups, nested groups |
| `accessShape.collections` / `groupGrants` / `directGrants` | `CreateShapedCollectionsStep` (replaces `CreateCollectionsStep`) | Histogram collection sizes (empties included), hot pairs, weighted group and direct grants |
| — | `GenerateCiphersStep` | Fills each collection to its target size when `accessShape` is present |
| `myItems` | `CreateMyItemsStep` | One `DefaultUserCollection` per confirmed member plus migrated org ciphers |
| `policies.enable` / `disable` / `data` | `CreatePoliciesStep` | `Policy` rows with per-type data (`enableAll` is still ignored) |
| `users.adminCount` | `CreateUsersStep` | The first N confirmed members are Admins |

All draws are seeded by `accessShape.seed`, so a preset reproduces its shape.

## Knobs, and what each one moves

Sizes use histograms: `[{ "min", "max", "weight" }]`. Bucket counts are exact, and values within a bucket are log-uniform.

- **`departments.count` / `locality`.** Members, groups and collections get a home department. Group membership,
  grants and multi-collection items stay in-department with probability `locality`. This creates path overlap.
- **`groups.hotGroupSizes`, `hotGroupParents`, `hotGroupOverlap`, `groupGrants.hotCollections` / `hotPairs`.**
  Mid-sized groups granted very large collections. In real orgs this pairing dominates report rows. Hot groups can be
  nested in a chain (C inside B inside A). When they share collections, every common member gets one path per group,
  and that is where most duplicate sync rows come from.
- **`groupGrants.wideGroups`.** A few tiny groups granted nested prefixes of one block of near-empty collections
  (admin-style). They set the maximum collections a member reaches, and they make part of the block granted by many
  groups, without adding many items.
- **`groupGrants.grantedGroupRate`, `groupAppetiteAlpha`, `maxGrantsPerGroup`, `groupSizeBias`.** Which groups get random
  grants, how unevenly, and the cap. The ungranted groups are the smallest ones. Together these control median and P90
  reach and total group report rows.
- **`collectionPopularityAlpha`, `collectionSizeBias`.** Whether random grants pile onto a few collections, and onto big
  or small ones. Keep popularity mild: a real org's busiest collection is granted by about 20 groups.
- **`groups.nestedGroupRate` + `groupGrants.echoRate`.** Child groups are subsets of a larger parent, and echo grants
  copy a parent's grant to a child. These are secondary sources of duplicate paths.
- **`directGrants.collectionSizeBias`, `userAffinityAlpha`, `ungroupedBoost`.** The volume and concentration of direct
  report rows. `ungroupedBoost` sends direct access mostly to members outside team groups.
- **`users.adminCount`, `policies.enable` / `disable` / `data`.** Admin members, and the org's policy set with
  per-type data.
- **`myItems.nonEmptyRate`, `sizes`, `largest`.** The migration tail: the share of members who moved items, and the
  heaviest vaults.

## What `myItems` writes, and why it matches a real migration

- **Collections** match `Collection_CreateDefaultCollections`: `Type = 1`, no `ExternalId` and no
  `DefaultUserCollectionEmail`, created on the policy date. Each has one `CollectionUser` with `Manage = 1` and no group
  grants. All of them share one org-key ciphertext of the name, because the client encrypts the name once in the policy
  request.
- **Items** are what `PUT /ciphers/share` leaves behind: org ciphers encrypted with the org key, each in exactly one
  collection (the owner's My Items). `CreationDate` is before the policy date and `RevisionDate` is after it.
- **Policy.** `OrganizationDataOwnership` gets `{"enableIndividualItemsTransfer":true}`, and the Enterprise plan sets
  `UseMyItems`.

## Calibrating against a target org

Measure the target org's rows, not just its counts: report rows by grant source, sync rows per distinct item, member reach (P50/P90/P99/max), top groups by report rows, grants per group, group overlap and nesting, and how many groups grant each collection. Tune the knobs above until the seeded org matches.

## Verify

`density` verification queries check counts. For access shape, check the resulting **rows** instead: report rows split
by grant source, sync rows per distinct item, and per-member collection/item reach (P50/P90/P99/max). Compare them with
the numbers the preset was calibrated to.
