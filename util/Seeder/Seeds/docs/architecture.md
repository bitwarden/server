# Fixture & Preset Architecture

How the Seeder's JSON layers compose to create test data.

## The Core Rule

**Fixtures define what exists. Presets define how things relate.**

Fixtures are independent, reusable building blocks. They never reference each other. The preset is the only layer that sees all the pieces and defines cross-cutting relationships between them.

## Fixtures = Bricks

Each fixture type describes one category of entities:

| Fixture Type     | What It Defines                          | Knows About                     |
| ---------------- | ---------------------------------------- | ------------------------------- |
| **Organization** | Name, domain                             | Nothing else                    |
| **Roster**       | Users, groups, collections, permissions  | Its own users (by email prefix) |
| **Ciphers**      | Vault items (logins, cards, notes, etc.) | Nothing else                    |

Fixtures are stored under `fixtures/organizations/`, `fixtures/rosters/`, and `fixtures/ciphers/`. A roster knows about its own users (groups reference members by email prefix, collections reference groups and users) but has zero knowledge of which organization or ciphers it'll be paired with.

## Presets = Assembly Instructions

A preset picks one of each fixture (or generates data inline) and defines the relationships between them. It's the entry point for the `--preset` CLI flag.

There are three composition modes:

- **Fixture-based** — pointers to existing JSON files. See `presets/qa/enterprise-basic.json`.
- **Generation-based** — inline counts + density parameters, no fixtures. See `presets/scale/md-balanced-sterling-cooper.json`.
- **Hybrid** — mix of fixture and generated data. See `presets/qa/families-basic.json`.

The schemas are the source of truth for what fields are available: `schemas/preset.schema.json`, `schemas/roster.schema.json`, `schemas/cipher.schema.json`.

## Cross-Cutting Relationships

The preset owns all relationships that cross fixture boundaries. Three assignment types are supported:

### Collection Assignments (org-scoped)

`collectionAssignments` maps `(cipher, collection)` tuples — which ciphers belong in which collections. Collections are defined in the roster; ciphers are defined in the cipher fixture. When present, replaces the default round-robin cipher-to-collection assignment. Without it, `CreateCiphersStep` distributes ciphers across collections via round-robin.

### Folder Assignments (user-scoped)

Folder **declarations** go in the roster — each user can optionally declare a `folders` array of named folders. For individual user presets (no roster), folder declarations use the preset-level `folderNames` array instead. Folder **assignments** go in the preset — `folderAssignments` maps `(cipher, user, folder)` tuples, mirroring the `Cipher.Folders` JSON column: `{"USERID":"FOLDERID"}`.

### Favorite Assignments (user-scoped)

`favoriteAssignments` maps `(cipher, user)` tuples, mirroring `Cipher.Favorites`: `{"USERID":true}`.

See `presets/qa/zero-knowledge-labs-enterprise.json` for a working example with all three assignment types.

## Why This Design

1. **Honest to the data model** — assignment arrays mirror the actual DB relationships (`CollectionCipher`, `Cipher.Folders` JSON, `Cipher.Favorites` JSON)
2. **No mixed concerns** — roster owns entities, cipher fixture owns vault items, preset owns relationships
3. **Reusable bricks** — the same cipher fixture pairs with any roster and any assignment configuration
4. **Backward compatible** — presets without assignment arrays continue to use round-robin collection distribution and have no folder/favorite data

## The DB Analogy

| Fixture      | Like This Table                                     |
| ------------ | --------------------------------------------------- |
| Organization | `Organization`                                      |
| Roster       | `User` + `Group` + `Collection` + join tables       |
| Ciphers      | `Cipher`                                            |
| Preset       | The `JOIN` — picks tables and defines relationships |
