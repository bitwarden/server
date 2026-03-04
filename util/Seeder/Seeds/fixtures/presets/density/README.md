# Density Preset Profiles

These presets model real production organization archetypes with calibrated density parameters. Each preset creates a fictional org with relationship patterns (group membership, collection fan-out, permission distribution, cipher assignment) that match production data profiles.

Always use the `--mangle` flag to avoid collisions with existing data.

## Usage

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-{preset-name} --mangle
```

## Verification Queries

Run the first query to get the Organization ID, then paste it into the remaining queries.

### Q0: Find the Organization ID

```sql
SELECT Id, [Name]
FROM [dbo].[Organization] WITH (NOLOCK)
WHERE [Name] = 'PASTE_ORG_NAME_HERE';
```

### Q1: Group Membership Distribution

Verifies the `membership.shape` and `membership.skew` parameters. The member counts across groups should reflect the configured distribution: roughly equal for Uniform, decaying for PowerLaw, or one dominant group for MegaGroup.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    G.[Name],
    COUNT(GU.OrganizationUserId) AS Members
FROM [dbo].[Group] G WITH (NOLOCK)
LEFT JOIN [dbo].[GroupUser] GU WITH (NOLOCK) ON G.Id = GU.GroupId
WHERE G.OrganizationId = @OrgId
GROUP BY G.[Name]
ORDER BY Members DESC;
```

### Q2: CollectionGroup Count

Verifies the `collectionFanOut` parameters. The total CollectionGroup record count should fall within `collections * min` to `collections * max`, reflecting how many groups each collection is assigned to.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT COUNT(*) AS CollectionGroupCount
FROM [dbo].[CollectionGroup] CG WITH (NOLOCK)
INNER JOIN [dbo].[Collection] C WITH (NOLOCK) ON CG.CollectionId = C.Id
WHERE C.OrganizationId = @OrgId;
```

### Q3: Permission Distribution

Verifies the `permissions` weights. The percentage breakdown of ReadOnly, ReadWrite, Manage, and HidePasswords across both CollectionUser and CollectionGroup records should approximate the configured weights. Zero-weight permissions must produce zero records.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    'CollectionUser' AS [Source],
    COUNT(*) AS Total,
    SUM(CASE WHEN CU.ReadOnly = 1 THEN 1 ELSE 0 END) AS ReadOnly,
    SUM(CASE WHEN CU.Manage = 1 THEN 1 ELSE 0 END) AS Manage,
    SUM(CASE WHEN CU.HidePasswords = 1 THEN 1 ELSE 0 END) AS HidePasswords,
    SUM(CASE WHEN CU.ReadOnly = 0 AND CU.Manage = 0 AND CU.HidePasswords = 0 THEN 1 ELSE 0 END) AS ReadWrite
FROM [dbo].[CollectionUser] CU WITH (NOLOCK)
INNER JOIN [dbo].[OrganizationUser] OU WITH (NOLOCK) ON CU.OrganizationUserId = OU.Id
WHERE OU.OrganizationId = @OrgId
UNION ALL
SELECT
    'CollectionGroup',
    COUNT(*),
    SUM(CASE WHEN CG.ReadOnly = 1 THEN 1 ELSE 0 END),
    SUM(CASE WHEN CG.Manage = 1 THEN 1 ELSE 0 END),
    SUM(CASE WHEN CG.HidePasswords = 1 THEN 1 ELSE 0 END),
    SUM(CASE WHEN CG.ReadOnly = 0 AND CG.Manage = 0 AND CG.HidePasswords = 0 THEN 1 ELSE 0 END)
FROM [dbo].[CollectionGroup] CG WITH (NOLOCK)
INNER JOIN [dbo].[Collection] C WITH (NOLOCK) ON CG.CollectionId = C.Id
WHERE C.OrganizationId = @OrgId;
```

### Q4: Orphan Ciphers

Verifies the `cipherAssignment.orphanRate`. Orphan ciphers belong to the organization but have no CollectionCipher assignment, so they are invisible to non-admin users.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    COUNT(*) AS TotalCiphers,
    SUM(CASE WHEN CC.CipherId IS NULL THEN 1 ELSE 0 END) AS Orphans
FROM [dbo].[Cipher] CI WITH (NOLOCK)
LEFT JOIN (SELECT DISTINCT CipherId FROM [dbo].[CollectionCipher] WITH (NOLOCK)) CC
    ON CI.Id = CC.CipherId
WHERE CI.OrganizationId = @OrgId;
```

### Q5: Direct Access Ratio

Verifies the `directAccessRatio` parameter. This controls what fraction of confirmed org users receive direct CollectionUser records (vs accessing collections only through group membership). The ratio is `int(userCount * directAccessRatio) / userCount`, so small orgs may show truncation error.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    TotalOrgUsers,
    UsersWithDirectAccess,
    CAST(UsersWithDirectAccess AS FLOAT) / NULLIF(TotalOrgUsers, 0) AS DirectAccessRatio
FROM (
    SELECT
        (SELECT COUNT(*) FROM [dbo].[OrganizationUser] WITH (NOLOCK)
         WHERE OrganizationId = @OrgId AND [Status] = 2) AS TotalOrgUsers,
        (SELECT COUNT(DISTINCT CU.OrganizationUserId)
         FROM [dbo].[CollectionUser] CU WITH (NOLOCK)
         INNER JOIN [dbo].[OrganizationUser] OU WITH (NOLOCK) ON CU.OrganizationUserId = OU.Id
         WHERE OU.OrganizationId = @OrgId) AS UsersWithDirectAccess
) T;
```

### Q6: Cipher Distribution Shape

Verifies the `cipherAssignment.skew` parameter. The coefficient of variation (StdDev / Mean) distinguishes distribution shapes: near zero means ciphers are spread uniformly across collections, while a high value indicates a heavyRight skew where a few collections hold most ciphers.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    COUNT(*) AS Collections,
    MIN(CipherCount) AS MinCiphers,
    MAX(CipherCount) AS MaxCiphers,
    AVG(CipherCount) AS AvgCiphers,
    CASE WHEN AVG(CipherCount) > 0
        THEN STDEV(CipherCount) / AVG(CipherCount)
        ELSE 0
    END AS CoefficientOfVariation
FROM (
    SELECT C.Id, COUNT(CC.CipherId) AS CipherCount
    FROM [dbo].[Collection] C WITH (NOLOCK)
    LEFT JOIN [dbo].[CollectionCipher] CC WITH (NOLOCK) ON C.Id = CC.CollectionId
    WHERE C.OrganizationId = @OrgId
    GROUP BY C.Id
) T;
```

### Q7: Collections-Per-User Distribution

Verifies the `userCollections` distribution parameters. Shows how many direct collections each user has access to via CollectionUser records. The coefficient of variation distinguishes uniform (CV near 0) from power-law (CV > 0.5) user-collection assignment shapes.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    COUNT(*) AS UsersWithDirectAccess,
    MIN(CollectionCount) AS MinCollections,
    MAX(CollectionCount) AS MaxCollections,
    AVG(CollectionCount) AS AvgCollections,
    CASE WHEN AVG(CollectionCount) > 0
        THEN STDEV(CollectionCount) / AVG(CollectionCount)
        ELSE 0
    END AS CoefficientOfVariation
FROM (
    SELECT CU.OrganizationUserId, COUNT(DISTINCT CU.CollectionId) AS CollectionCount
    FROM [dbo].[CollectionUser] CU WITH (NOLOCK)
    INNER JOIN [dbo].[OrganizationUser] OU WITH (NOLOCK) ON CU.OrganizationUserId = OU.Id
    WHERE OU.OrganizationId = @OrgId
    GROUP BY CU.OrganizationUserId
) T;
```

### Q8: Multi-Collection Ciphers

Verifies the `cipherAssignment.multiCollectionRate` parameter. Counts how many non-orphan ciphers are assigned to more than one collection, and the maximum number of collections per cipher. The ratio of multi-collection ciphers to total assigned ciphers should approximate the configured `multiCollectionRate`.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT
    COUNT(*) AS TotalAssignedCiphers,
    SUM(CASE WHEN CollectionCount > 1 THEN 1 ELSE 0 END) AS MultiCollectionCiphers,
    MAX(CollectionCount) AS MaxCollectionsPerCipher
FROM (
    SELECT CC.CipherId, COUNT(DISTINCT CC.CollectionId) AS CollectionCount
    FROM [dbo].[CollectionCipher] CC WITH (NOLOCK)
    INNER JOIN [dbo].[Cipher] CI WITH (NOLOCK) ON CC.CipherId = CI.Id
    WHERE CI.OrganizationId = @OrgId
    GROUP BY CC.CipherId
) T;
```

---

## Preset Catalog

| Preset                                 | Tier | Archetype                   | Users  | Groups | Collections | Ciphers | Plan                |
| -------------------------------------- | ---- | --------------------------- | ------ | ------ | ----------- | ------- | ------------------- |
| density-xs-central-perk                | XS   | Family starter              | 6      | 2      | 10          | 200     | families-annually   |
| density-sm-balanced-planet-express      | SM   | Small balanced              | 50     | 8      | 100         | 750     | teams-annually      |
| density-sm-highperm-bluth-company       | SM   | Small hierarchical          | 50     | 4      | 25          | 500     | teams-annually      |
| density-md-balanced-sterling-cooper     | MD   | Mid-market balanced         | 250    | 50     | 500         | 5,000   | enterprise-annually |
| density-md-highcollection-umbrella-corp | MD   | Collection-heavy            | 200    | 8      | 800         | 3,000   | enterprise-annually |
| density-lg-balanced-wayne-enterprises   | LG   | Large balanced              | 1,000  | 100    | 2,000       | 10,000  | enterprise-annually |
| density-lg-highperm-tyrell-corp         | LG   | High permission density     | 2,500  | 75     | 2,300       | 17,000  | enterprise-annually |
| density-xl-highperm-weyland-yutani     | XL   | Mega corp, many groups      | 5,000  | 500    | 1,200       | 15,000  | enterprise-annually |
| density-xl-highcollection-buy-n-large  | XL   | Mega corp, many collections | 10,000 | 5      | 12,000      | 15,000  | enterprise-annually |

> **Note:** The XS preset uses `families-annually` plan, which sets `UseGroups = false` on the org entity. The Seeder creates groups regardless, but the Bitwarden web vault UI will not display groups for families-plan orgs.

---

## Presets

### 1. Central Perk (XS — Family Starter)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-xs-central-perk --mangle
```

| Check               | Expected                                                                     |
| ------------------- | ---------------------------------------------------------------------------- |
| Membership shape      | Uniform — 2 groups with ~3 members each.                                     |
| CollectionGroups      | 10-20 records. Uniform fan-out of 1-2 groups per collection across 2 groups. |
| Permissions           | ~50% Manage, ~40% ReadWrite, ~10% ReadOnly, 0% HidePasswords.                |
| Orphan ciphers        | 0 of 200 (0% orphan rate).                                                   |
| Direct access ratio   | 0.8 — ~80% of access paths are direct CollectionUser.                        |
| Collections per user  | Uniform 1-3. Min=1, Max=3, Avg=2.                                            |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                       |

### 2. Planet Express (S — Small Balanced)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-sm-balanced-planet-express --mangle
```

| Check               | Expected                                                                       |
| ------------------- | ------------------------------------------------------------------------------ |
| Membership shape      | PowerLaw (skew 0.4) — first group largest, gentle decay across 8 groups.       |
| CollectionGroups      | 200-400 records. Uniform fan-out of 2-4 groups per collection across 8 groups. |
| Permissions           | ~40% ReadOnly, ~30% ReadWrite, ~25% Manage, ~5% HidePasswords.                 |
| Orphan ciphers        | ~37 of 750 (5% orphan rate).                                                   |
| Direct access ratio   | 0.7 — ~70% of access paths are direct CollectionUser.                          |
| Collections per user  | PowerLaw 1-5 (skew 0.3). First users get up to 5, most get 1-2. CV > 0.3.     |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                         |

### 3. Bluth Company (S — Small Hierarchical)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-sm-highperm-bluth-company --mangle
```

| Check               | Expected                                                                       |
| ------------------- | ------------------------------------------------------------------------------ |
| Membership shape      | PowerLaw (skew 0.7) — steep decay across 4 groups. First group dominant.       |
| CollectionGroups      | 25-125 records. PowerLaw fan-out of 1-5 groups per collection across 4 groups. |
| Permissions           | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords.                   |
| Orphan ciphers        | ~75 of 500 (15% orphan rate).                                                  |
| Direct access ratio   | 0.6 — ~60% of access paths are direct CollectionUser.                          |
| Collections per user  | Uniform 1-3. Min=1, Max=3, Avg=2.                                              |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                         |

### 4. Sterling Cooper (M — Mid-Market Balanced)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-md-balanced-sterling-cooper --mangle
```

| Check               | Expected                                                                 |
| ------------------- | ------------------------------------------------------------------------ |
| Membership shape      | PowerLaw (skew 0.6) — moderate decay across 50 groups.                   |
| CollectionGroups      | 500-2,500 records. PowerLaw fan-out of 1-5 active groups per collection. |
| Permissions           | ~55% ReadOnly, ~20% ReadWrite, ~15% Manage, ~10% HidePasswords.          |
| Orphan ciphers        | ~400 of 5,000 (8% orphan rate).                                          |
| Direct access ratio   | 0.5 — roughly even split between direct and group-mediated access.       |
| Empty group rate      | ~26% — ~13 of 50 groups have 0 members due to power-law tail truncation. |
| Collections per user  | PowerLaw 1-10 (skew 0.5). First users get up to 10, most get 1-2. CV > 0.5. |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                   |

### 5. Umbrella Corp (M — Collection-Heavy)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-md-highcollection-umbrella-corp --mangle
```

| Check               | Expected                                                                                                |
| ------------------- | ------------------------------------------------------------------------------------------------------- |
| Membership shape      | MegaGroup (skew 0.5) — group 1 has ~72% of members, remaining 7 split the rest evenly.                  |
| CollectionGroups      | 800-2,400 records. FrontLoaded fan-out of 1-3 groups per collection. First collections get more groups. |
| Permissions           | ~50% ReadWrite, ~20% Manage, ~20% ReadOnly, ~10% HidePasswords.                                         |
| Orphan ciphers        | ~600 of 3,000 (20% orphan rate).                                                                        |
| Direct access ratio   | 0.9 — ~90% of access paths are direct CollectionUser.                                                   |
| Collections per user  | PowerLaw 1-15 (skew 0.6). First users get up to 15, most get 1-2. CV > 0.5.                             |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                                                   |

### 6. Wayne Enterprises (L — Large Balanced)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-lg-balanced-wayne-enterprises --mangle
```

| Check               | Expected                                                                       |
| ------------------- | ------------------------------------------------------------------------------ |
| Membership shape      | PowerLaw (skew 0.7) — steep decay across 100 groups. First groups much larger. |
| CollectionGroups      | 2,000-10,000 records. PowerLaw fan-out of 1-5 active groups per collection.    |
| Permissions           | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords.                   |
| Orphan ciphers        | ~1,000 of 10,000 (10% orphan rate).                                            |
| Direct access ratio   | 0.5 — roughly even split between direct and group-mediated access.             |
| Empty group rate      | ~30% — ~30 of 100 groups have 0 members due to power-law tail truncation.      |
| Collections per user  | PowerLaw 1-25 (skew 0.6). First users get up to 25, most get 1-2. CV > 0.5.   |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                         |

### 7. Tyrell Corp (L — High Permission Density)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-lg-highperm-tyrell-corp --mangle
```

| Check               | Expected                                                                         |
| ------------------- | -------------------------------------------------------------------------------- |
| Membership shape      | PowerLaw (skew 0.8) — very steep decay across 75 groups. First group very large. |
| CollectionGroups      | 4,600-18,400 records. PowerLaw fan-out of 2-8 active groups per collection.      |
| Permissions           | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords.                     |
| Orphan ciphers        | ~2,550 of 17,000 (15% orphan rate).                                              |
| Direct access ratio   | 0.6 — ~60% of access paths are direct CollectionUser.                            |
| Empty group rate      | 20% — ~15 of 75 groups have 0 members and are excluded from fan-out.             |
| Collections per user  | PowerLaw 1-30 (skew 0.7). First users get up to 30, most get 1-2. CV > 0.5.     |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                           |

### 8. Weyland-Yutani (XL — Mega Corp, Many Groups)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-xl-highperm-weyland-yutani --mangle
```

| Check               | Expected                                                                             |
| ------------------- | ------------------------------------------------------------------------------------ |
| Membership shape      | PowerLaw (skew 0.8) — very steep decay across 500 groups. Long tail of small groups. |
| CollectionGroups      | 1,200-3,600 records. PowerLaw fan-out of 1-3 active groups per collection.           |
| Permissions           | ~55% ReadWrite, ~25% ReadOnly, ~10% Manage, ~10% HidePasswords.                      |
| Orphan ciphers        | ~1,500 of 15,000 (10% orphan rate).                                                  |
| Direct access ratio   | 0.4 — majority of access is group-mediated.                                          |
| Empty group rate      | ~68% — ~341 of 500 groups have 0 members due to power-law tail truncation.           |
| Collections per user  | PowerLaw 1-50 (skew 0.8). First users get up to 50, most get 1-2. CV > 0.5.         |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                               |

### 9. Buy n Large (XL — Mega Corp, Many Collections)

```bash
cd util/SeederUtility
dotnet run -- seed --preset density.density-xl-highcollection-buy-n-large --mangle
```

| Check               | Expected                                                                                                    |
| ------------------- | ----------------------------------------------------------------------------------------------------------- |
| Membership shape      | MegaGroup (skew 0.95) — group 1 has ~93% of members, remaining 4 split the rest evenly.                     |
| CollectionGroups      | 0 records. DirectAccessRatio is 1.0, so CollectionGroup creation is skipped entirely.                       |
| Permissions           | ~30% Manage, ~30% ReadWrite, ~30% ReadOnly, ~10% HidePasswords.                                             |
| Orphan ciphers        | ~12,750 of 15,000 (85% orphan rate).                                                                        |
| Direct access ratio   | 1.0 — 100% of access paths are direct CollectionUser (all users get individual records).                    |
| Collections per user  | PowerLaw 1-20 (skew 0.5). First users get up to 20, most get 1. CV > 0.2.                                  |
| Multi-collection rate | 0 — no multiCollectionRate configured.                                                                      |
