# Preset Catalog

Complete catalog of all seeder presets, organized by purpose. Use `--mangle` to avoid collisions with existing data.

## Features

Test specific Bitwarden features. Fixture-based data for deterministic results.

```bash
dotnet run -- seed --preset features.{name} --mangle
```

| Preset            | Plan                | Features Enabled                                   | Org Fixture      | Roster       | Ciphers   |
| ----------------- | ------------------- | -------------------------------------------------- | ---------------- | ------------ | --------- |
| sso-enterprise    | enterprise-annually | SSO (OIDC, masterPassword) + requireSso policy     | verdant-health   | starter-team | sso-vault |
| tde-enterprise    | enterprise-annually | SSO (OIDC, trustedDevices/TDE) + requireSso policy | obsidian-labs    | starter-team | tde-vault |
| policy-enterprise | enterprise-annually | All policies except requireSso and require2fa      | pinnacle-designs | starter-team | —         |

`policy-enterprise` has no ciphers — it exists purely for testing policy enforcement.

## QA

Known users, groups, collections, and permissions you can point a client to.

```bash
dotnet run -- seed --preset qa.{name} --mangle
```

| Preset                            | Plan                | Seats | Org Fixture       | Roster                 | Ciphers                | Use Case                           |
| --------------------------------- | ------------------- | ----- | ----------------- | ---------------------- | ---------------------- | ---------------------------------- |
| enterprise-basic                  | enterprise-annually | 10    | redwood-analytics | enterprise-basic       | enterprise-basic       | Standard enterprise org            |
| collection-permissions-enterprise | enterprise-annually | 10    | cobalt-logistics  | collection-permissions | collection-permissions | Permission edge cases              |
| dunder-mifflin-enterprise-full    | enterprise-annually | 70    | dunder-mifflin    | dunder-mifflin         | autofill-testing       | Large handcrafted org              |
| families-basic                    | families-annually   | 6     | adams-family      | family                 | 150 generated          | Families plan with personal vaults |
| stark-free-basic                  | free                | 2     | stark-industries  | 1 generated user       | autofill-testing       | Free plan personal vault           |

`families-basic` and `stark-free-basic` mix fixtures with generated data (ciphers and personal ciphers).

## Scale

Production-calibrated presets with density modeling. Realistic relationship patterns (group membership, collection fan-out, permission distribution, cipher assignment) across 5 tiers.

```bash
dotnet run -- seed --preset scale.{name} --mangle
```

| Preset                          | Tier | Archetype                   | Users  | Groups | Collections | Ciphers | Plan                |
| ------------------------------- | ---- | --------------------------- | ------ | ------ | ----------- | ------- | ------------------- |
| xs-central-perk                 | XS   | Family starter              | 6      | 2      | 10          | 200     | families-annually   |
| sm-balanced-planet-express      | SM   | Small balanced              | 50     | 8      | 100         | 750     | teams-annually      |
| sm-highperm-bluth-company       | SM   | Small hierarchical          | 50     | 4      | 25          | 500     | teams-annually      |
| md-balanced-sterling-cooper     | MD   | Mid-market balanced         | 250    | 50     | 500         | 5,000   | enterprise-annually |
| md-highcollection-umbrella-corp | MD   | Collection-heavy            | 200    | 8      | 800         | 3,000   | enterprise-annually |
| lg-balanced-wayne-enterprises   | LG   | Large balanced              | 1,000  | 100    | 2,000       | 10,000  | enterprise-annually |
| lg-highperm-tyrell-corp         | LG   | High permission density     | 2,500  | 75     | 2,300       | 17,000  | enterprise-annually |
| xl-highperm-weyland-yutani      | XL   | Mega corp, many groups      | 5,000  | 500    | 1,200       | 15,000  | enterprise-annually |
| xl-broad-initech                | XL   | Mega corp, many collections | 10,000 | 5      | 12,000      | 15,000  | enterprise-annually |

**Notes:**

- The XS preset uses `families-annually`, which hides the Groups UI even though the seeder creates groups.
- **Cipher types**: Most use `realistic` (60% Login, 15% SecureNote, 12% Card, 10% Identity, 3% SSHKey). Umbrella Corp uses `documentationHeavy` (40/40 Login/SecureNote). Tyrell Corp uses `developerFocused` (50% Login, 20% SSHKey).
- **Personal ciphers**: Sterling Cooper and Wayne Enterprises use `realistic` distribution. Weyland-Yutani uses `lightUsage`. Use `heavyUsage` only for small/mid orgs — at XL scale it produces 300K+ ciphers and will timeout.
- **Folders**: Wayne Enterprises uses `enterprise` folder distribution. Weyland-Yutani uses `minimal`.

For per-preset expected values and verification queries, see [verification.md](verification.md).

## Validation

Algorithm verification for seeder development. Not for general use.

```bash
dotnet run -- seed --preset validation.{name} --mangle
```

| Preset                             | Tests                                                         |
| ---------------------------------- | ------------------------------------------------------------- |
| density-modeling-power-law-test    | PowerLaw group membership, fan-out, permissions, orphans      |
| density-modeling-mega-group-test   | MegaGroup membership, FrontLoaded fan-out, all-group access   |
| density-modeling-empty-groups-test | EmptyGroupRate exclusion from CollectionGroup                 |
| density-modeling-no-density-test   | Backward compatibility (no density block = original behavior) |

For expected values and verification queries, see [verification.md](verification.md).
