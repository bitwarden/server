# Access shape, My Items and policies

Three preset blocks for reproducing a **real** large organization rather than an archetype. They exist because org
load depends on *how* members reach items, not on item count. Access Intelligence downloads every org item (My Items
and trash included), then maps each item to every member who reaches it through a shared collection, whatever the
member's status. Sync returns one row per item × collection × access path, so overlapping groups multiply it. The
older member-access (DIRT) report follows the same paths, one row per item × collection × path. The `density` algorithms can't express that shape: each user is in one group,
collection sizes are near-uniform, and grants are round-robin. Reference preset:
[`scale/xl-migrated-cyberdyne.json`](../fixtures/presets/scale/xl-migrated-cyberdyne.json).

| Block | Step | What it adds |
| --- | --- | --- |
| `users.statusMix` | `CreateUsersStep` | Exact confirmed / invited / accepted / revoked weights |
| `accessShape.groups` | `CreateShapedGroupsStep` (replaces `CreateGroupsStep`) | Everyone group, hot groups, long-tail sizes, members in up to `maxGroupsPerUser` groups, nested groups |
| `accessShape.collections` / `groupGrants` / `directGrants` | `CreateShapedCollectionsStep` (replaces `CreateCollectionsStep`) | Histogram collection sizes (empties included), hot pairs, weighted group and direct grants |
| `accessShape.inactiveAccessRate` | `CreateShapedCollectionsStep` | Invited, accepted and revoked members who keep group memberships and direct grants |
| — | `GenerateCiphersStep` | Fills each collection to its target size when `accessShape` is present |
| `myItems` | `CreateMyItemsStep` | One `DefaultUserCollection` per confirmed member plus migrated org ciphers, some of them trashed |
| `policies.enable` / `disable` / `data` | `CreatePoliciesStep` | `Policy` rows with per-type data, checked against the server's policy dependencies |
| `users.adminCount` | `CreateUsersStep` | The first N confirmed members are Admins |

All draws are seeded by `accessShape.seed`, so a preset reproduces its shape. `PresetValidator` rejects density
settings that `accessShape` would ignore, so a preset uses one model at a time, and the shaped loops fail the run if
they fall more than 1% short of a target.

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
- **`inactiveAccessRate`.** The share of invited, accepted and revoked members who copy a random confirmed member's
  group memberships (except the everyone group) and direct grants. Real orgs leave revoked members in their groups, and
  Access Intelligence maps them, so this moves member-item pairs but not sync. It runs on its own random stream, so it
  doesn't change the confirmed members' shape.
- **`users.adminCount`, `policies.enable` / `disable` / `data`.** Admin members, and the org's policy set with
  per-type data.
- **`myItems.nonEmptyRate`, `sizes`, `largest`.** The migration tail: the share of members who moved items, and the
  heaviest vaults.
- **`myItems.deletedRate` with `cipherAssignment.deletedRate` / `maxDeletedCiphers`.** Where the trash sits. In a
  migrated org most trashed items are in My Items. Trash in My Items comes on top of the active items, while pool trash
  takes slots in shared collections, so lowering pool trash raises active shared rows unless `totalAssignments` and
  `hotCollections` come down with it.

## What `myItems` writes, and why it matches a real migration

- **Collections** match `Collection_CreateDefaultCollections`: `Type = 1`, no `ExternalId` and no
  `DefaultUserCollectionEmail`, created on the policy date. Only confirmed User and Custom members get one: the policy
  exempts Owners and Admins. Each has one `CollectionUser` with `Manage = 1` and no group
  grants. All of them share one org-key ciphertext of the name, because the client encrypts the name once in the policy
  request.
- **Items** are what `PUT /ciphers/share` leaves behind: org ciphers encrypted with the org key, each in exactly one
  collection (the owner's My Items). `CreationDate` is before the policy date and `RevisionDate` is after it.
  `deletedRate` adds trashed items, deleted after they migrated.
- **Policy.** `OrganizationDataOwnership` gets `{"enableIndividualItemsTransfer":true}`, and the Enterprise plan sets
  `UseMyItems`.

## Calibrating against a target org

Measure the target org's rows, not just its counts. The load metrics, in order:

1. **Items fetched**: every org cipher, My Items and trash included.
2. **Member-item pairs**: distinct (member, item) through shared collections, members of every status, split by status.
   This is what Access Intelligence maps today. Items in My Items map to no member on that path.
3. **Sync rows per distinct item** and per-member reach (P50/P90/P99/max, and P99 sync rows).

For the structure behind them, also measure groups per member, top groups by member-item pairs, grants per group,
group overlap and nesting, and how many groups grant each collection. Member-access report rows by grant source are a
useful secondary check. Tune the knobs above until the seeded org matches.

## Verify

`density` verification queries check counts. For access shape, check the resulting load instead: items fetched,
member-item pairs by member status, sync rows per distinct item, and per-member collection/item reach
(P50/P90/P99/max). [verification.md](verification.md#q12-access-shape-load) has the query. Compare the results with the
numbers the preset was calibrated to.
