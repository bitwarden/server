# Preset Catalog

Complete catalog of all seeder presets, organized by purpose. Use `--mangle` to avoid collisions with existing data.

## Cipher generation knobs

These options apply to any preset that uses generated (count-based) ciphers — QA, Scale, and Individual alike. Add them to the `"ciphers"` or `"personalCiphers"` block in the preset JSON. Schema reference: `Seeds/schemas/preset.schema.json`.

| Knob                     | Type    | Default | Description                                                                                                                                   |
| ------------------------ | ------- | ------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| `repromptEveryNthCipher` | integer | 0       | Set `Reprompt=Password` on every Nth generated cipher. `0` = disabled. Example: `5` flags ciphers at indices 0, 5, 10, … ≈ 20% reprompt rate. |

## Features

Test specific Bitwarden features. Fixture-based data for deterministic results.

```bash
dotnet run -- preset --name features.{name} --mangle
```

| Preset            | Features Enabled                                            | Org Fixture      | Roster           | Ciphers          |
| ----------------- | ----------------------------------------------------------- | ---------------- | ---------------- | ---------------- |
| sso-enterprise    | requireSso policy (OIDC SSO config is not seeded by the Seeder yet) | verdant-health   | starter-team     | enterprise-basic |
| tde-enterprise    | requireSso policy (OIDC SSO config is not seeded by the Seeder yet) | obsidian-labs    | starter-team     | enterprise-basic |
| local-sso         | SSO (SAML 2.0, masterPassword) — golden local-IdP login org | verdant-health   | enterprise-basic | enterprise-basic |
| policy-enterprise | All policies except requireSso and require2fa               | pinnacle-designs | starter-team     | —                |

`policy-enterprise` has no ciphers — it exists purely for testing policy enforcement.

`local-sso` is the golden clean-slate SAML 2.0 SSO org (fixed GUID; seed **without** `--mangle`). Log in via the bundled SimpleSAMLphp IdP as `owner` / `password` → `dana.whitfield@verdant.example`, who owns the department collections so the vault is populated on first login. Because the `enterprise-basic` cipher fixture carries attachments, Azurite (or a local attachment dir) must be configured to seed it. Wiring: `dev/authsources.php.example`, `dev/.env.example`.

## QA

Known users, groups, collections, and permissions you can point a client to.

```bash
dotnet run -- preset --name qa.{name} --mangle
```

| Preset                            | Org Fixture          | Roster                 | Ciphers                | Use Case                                          |
| --------------------------------- | -------------------- | ---------------------- | ---------------------- | ------------------------------------------------- |
| enterprise-basic                  | redwood-analytics    | enterprise-basic       | enterprise-basic       | Standard enterprise org                           |
| collection-permissions-enterprise | cobalt-logistics     | collection-permissions | collection-permissions | Permission edge cases                             |
| dunder-mifflin-enterprise-full    | dunder-mifflin       | dunder-mifflin         | autofill-testing       | Large handcrafted org                             |
| families-basic                    | adams-family         | family                 | 150 generated          | Families plan with personal vaults + reprompt     |
| stark-free-basic                  | stark-industries     | 1 generated user       | autofill-testing       | Free plan personal vault                          |
| zero-knowledge-labs-enterprise    | zero-knowledge-labs  | zero-knowledge-labs    | zero-knowledge-labs    | Full ZKL org with named folders + favorites       |
| paper-trail-partners-team         | paper-trail-partners | paper-trail-partners   | encryption-modes (34)  | Teams org: encryption modes across a shared vault |

`families-basic` and `stark-free-basic` mix fixtures with generated data (ciphers and personal ciphers).

`zero-knowledge-labs-enterprise` uses all three assignment types: `collectionAssignments` (ciphers to department collections), `folderAssignments` (per-user folder organization), and `favoriteAssignments` (per-user favorites).

`qa.paper-trail-partners-team` seeds the same `encryption-modes` fixture as `individual.encryption-modes`, but into a Teams-plan org (**Paper Trail Partners**) whose owner sits in an **All-Access** group, so the owner sees all 34 ciphers. Log in as `trail.owner@papertrail.example`.

## Scale

Production-calibrated presets with density modeling. Realistic relationship patterns (group membership, collection fan-out, permission distribution, cipher assignment) across 5 tiers.

```bash
dotnet run -- preset --name scale.{name} --mangle
```

| Preset                          | Tier | Archetype                   | Users  | Groups | Collections | Ciphers |
| ------------------------------- | ---- | --------------------------- | ------ | ------ | ----------- | ------- |
| xs-central-perk                 | XS   | Family starter              | 6      | 2      | 10          | 200     |
| sm-balanced-planet-express      | SM   | Small balanced              | 50     | 8      | 100         | 750     |
| sm-highperm-bluth-company       | SM   | Small hierarchical          | 50     | 4      | 25          | 500     |
| md-balanced-sterling-cooper     | MD   | Mid-market balanced         | 250    | 50     | 500         | 5,000   |
| md-highcollection-umbrella-corp | MD   | Collection-heavy            | 200    | 8      | 800         | 3,000   |
| lg-balanced-wayne-enterprises   | LG   | Large balanced              | 1,000  | 100    | 2,000       | 10,000  |
| lg-highperm-tyrell-corp         | LG   | High permission density     | 2,500  | 75     | 2,300       | 17,000  |
| xl-highperm-weyland-yutani      | XL   | Mega corp, many groups      | 5,000  | 500    | 1,200       | 15,000  |
| xl-broad-initech                | XL   | Mega corp, many collections | 10,000 | 5      | 12,000      | 15,000  |

**Notes:**

- The XS preset uses `families-annually`, which hides the Groups UI even though the seeder creates groups.
- **Cipher types**: Most use `realistic` (60% Login, 15% SecureNote, 12% Card, 10% Identity, 3% SSHKey). Umbrella Corp uses `documentationHeavy` (40/40 Login/SecureNote). Tyrell Corp uses `developerFocused` (50% Login, 20% SSHKey).
- **Personal ciphers**: Sterling Cooper and Wayne Enterprises use `realistic` distribution. Weyland-Yutani uses `lightUsage`. Use `heavyUsage` only for small/mid orgs — at XL scale it produces 300K+ ciphers and will timeout.
- **Folders**: Wayne Enterprises uses `enterprise` folder distribution. Weyland-Yutani uses `minimal`.
- **Archive & delete**: All nine presets set `cipherAssignment.deletedRate` (2-5%, capped at 25) and `archivedRate` (4-8%, capped at 50), tuned to each org's orphan-rate/permission story — tidier orgs (Central Perk) trend lower, locked-down/hierarchical orgs (Bluth Company, Tyrell Corp) and Initech's high-orphan mega-dump trend higher. Both rates apply to org ciphers (archived-for a round-robin-selected org member); the three presets with personal ciphers enabled (Sterling Cooper, Wayne Enterprises, Weyland-Yutani) apply the same rates a second time against their personal-cipher pool, so those three seed archived/deleted items in both places.

For per-preset expected values and verification queries, see [verification.md](verification.md).

## Individual

Individual user accounts with no organization. Useful for testing personal vault features.

```bash
dotnet run -- preset --name individual.{name} --mangle
```

| Preset           | Account Type  | Folders | Ciphers      | Assignments |
| ---------------- | ------------- | ------- | ------------ | ----------- |
| free             | Free          | —       | 0            | —           |
| premium          | Premium (1GB) | —       | 0            | —           |
| blob-migration   | Premium (1GB) | —       | 7 (fixture)  | —           |
| encryption-modes | Premium (1GB) | —       | 34 (fixture) | —           |

`free` and `premium` create accounts with no vault data — useful for testing account setup flows. Cipher count is set to 0 (TBD).

`encryption-modes` seeds 34 ciphers spanning all eight cipher types (login, card, identity, secure note, SSH key, bank account, driver's license, passport), both cipher-encryption modes (user-key and cipher-key), all three attachment schemes (v0/v1/v2), and some archived/deleted items. The same fixture is seeded into an org vault by `qa.paper-trail-partners-team` (see QA above).

**Login emails:** `free` uses `freeuser@individual.example`; `premium` uses `premuser@individual.example`; `blob-migration` uses `blobmigration@individual.example`; `encryption-modes` uses `encryptionmodes@individual.example`.

## Validation

Algorithm verification for seeder development. Not for general use.

```bash
dotnet run -- preset --name validation.{name} --mangle
```

| Preset                             | Tests                                                         |
| ---------------------------------- | ------------------------------------------------------------- |
| density-modeling-power-law-test    | PowerLaw group membership, fan-out, permissions, orphans      |
| density-modeling-mega-group-test   | MegaGroup membership, FrontLoaded fan-out, all-group access   |
| density-modeling-empty-groups-test | EmptyGroupRate exclusion from CollectionGroup                 |
| density-modeling-no-density-test   | Backward compatibility (no density block = original behavior) |

For expected values and verification queries, see [verification.md](verification.md).
