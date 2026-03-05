# Verification Queries

SQL queries for verifying density algorithm output against expected values. Run after seeding a scale or validation preset. These are developer-facing — not needed for normal seeder usage.

Run Q0 first to get the Organization ID, then paste it into the remaining queries.

## Queries

### Q0: Find the Organization ID

```sql
SELECT Id, [Name]
FROM [dbo].[Organization] WITH (NOLOCK)
WHERE [Name] = 'PASTE_ORG_NAME_HERE';
```

### Q1: Group Membership Distribution

Verifies `membership.shape` and `membership.skew`. Member counts should reflect Uniform (roughly equal), PowerLaw (decaying), or MegaGroup (one dominant).

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

Verifies `collectionFanOut`. Total should fall within `collections * min` to `collections * max`.

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT COUNT(*) AS CollectionGroupCount
FROM [dbo].[CollectionGroup] CG WITH (NOLOCK)
INNER JOIN [dbo].[Collection] C WITH (NOLOCK) ON CG.CollectionId = C.Id
WHERE C.OrganizationId = @OrgId;
```

### Q3: Permission Distribution

Verifies `permissions` weights. Zero-weight permissions must produce zero records.

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

Verifies `cipherAssignment.orphanRate`. Orphans have no CollectionCipher assignment.

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

Verifies `directAccessRatio`. Ratio is `int(userCount * directAccessRatio) / userCount`, so small orgs may show truncation.

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

Verifies `cipherAssignment.skew`. CV near 0 = uniform, CV high = heavyRight.

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

Verifies `userCollections`. CV near 0 = uniform, CV > 0.5 = power-law.

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

Verifies `cipherAssignment.multiCollectionRate`. Ratio should approximate the configured rate.

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

## Scale Preset Expected Values

### 1. Central Perk (XS)

| Check | Expected |
|-------|----------|
| Membership shape | Uniform — 2 groups with ~3 members each. |
| CollectionGroups | 10-20 records. Uniform fan-out of 1-2 groups per collection. |
| Permissions | ~50% Manage, ~40% ReadWrite, ~10% ReadOnly, 0% HidePasswords. |
| Orphan ciphers | 0 of 200 (0% orphan rate). |
| Direct access ratio | 0.8 — ~80% of access paths are direct CollectionUser. |
| Collections per user | Uniform 1-3. Min=1, Max=3, Avg=2. |
| Multi-collection rate | 20% of 200 non-orphan ciphers in 2 collections. ~40 multi-collection ciphers. |

### 2. Planet Express (SM)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.4) — first group largest, gentle decay across 8 groups. |
| CollectionGroups | 200-400 records. Uniform fan-out of 2-4 groups per collection. |
| Permissions | ~40% ReadOnly, ~30% ReadWrite, ~25% Manage, ~5% HidePasswords. |
| Orphan ciphers | ~37 of 750 (5% orphan rate). |
| Direct access ratio | 0.7 — ~70% of access paths are direct CollectionUser. |
| Collections per user | PowerLaw 1-5 (skew 0.3). First users get up to 5, most get 1-2. CV > 0.3. |
| Multi-collection rate | 15% of ~713 non-orphan ciphers in 2 collections. ~107 multi-collection ciphers. |

### 3. Bluth Company (SM)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.7) — steep decay across 4 groups. First group dominant. |
| CollectionGroups | 25-125 records. PowerLaw fan-out of 1-5 groups per collection. |
| Permissions | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords. |
| Orphan ciphers | ~75 of 500 (15% orphan rate). |
| Direct access ratio | 0.6 — ~60% of access paths are direct CollectionUser. |
| Collections per user | Uniform 1-3. Min=1, Max=3, Avg=2. |
| Multi-collection rate | 10% of ~425 non-orphan ciphers in 2 collections. ~42 multi-collection ciphers. |

### 4. Sterling Cooper (MD)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.6) — moderate decay across 50 groups. |
| CollectionGroups | 500-2,500 records. PowerLaw fan-out of 1-5 active groups per collection. |
| Permissions | ~55% ReadOnly, ~20% ReadWrite, ~15% Manage, ~10% HidePasswords. |
| Orphan ciphers | ~400 of 5,000 (8% orphan rate). |
| Direct access ratio | 0.5 — roughly even split between direct and group-mediated access. |
| Empty group rate | ~26% — ~13 of 50 groups have 0 members due to power-law tail truncation. |
| Collections per user | PowerLaw 1-10 (skew 0.5). First users get up to 10, most get 1-2. CV > 0.5. |
| Multi-collection rate | 20% of ~4,600 non-orphan ciphers in 2-3 collections. Max 3 per cipher. |

### 5. Umbrella Corp (MD)

| Check | Expected |
|-------|----------|
| Membership shape | MegaGroup (skew 0.5) — group 1 has ~72% of members, remaining 7 split the rest evenly. |
| CollectionGroups | 800-2,400 records. FrontLoaded fan-out of 1-3 groups per collection. |
| Permissions | ~50% ReadWrite, ~20% Manage, ~20% ReadOnly, ~10% HidePasswords. |
| Orphan ciphers | ~600 of 3,000 (20% orphan rate). |
| Direct access ratio | 0.9 — ~90% of access paths are direct CollectionUser. |
| Collections per user | PowerLaw 1-15 (skew 0.6). First users get up to 15, most get 1-2. CV > 0.5. |
| Multi-collection rate | 25% of ~2,400 non-orphan ciphers in 2-3 collections. Max 3 per cipher. |

### 6. Wayne Enterprises (LG)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.7) — steep decay across 100 groups. First groups much larger. |
| CollectionGroups | 2,000-10,000 records. PowerLaw fan-out of 1-5 active groups per collection. |
| Permissions | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords. |
| Orphan ciphers | ~1,000 of 10,000 (10% orphan rate). |
| Direct access ratio | 0.5 — roughly even split between direct and group-mediated access. |
| Empty group rate | ~30% — ~30 of 100 groups have 0 members due to power-law tail truncation. |
| Collections per user | PowerLaw 1-25 (skew 0.6). First users get up to 25, most get 1-2. CV > 0.5. |
| Multi-collection rate | 25% of ~9,000 non-orphan ciphers in 2-4 collections. Max 4 per cipher. |

### 7. Tyrell Corp (LG)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.8) — very steep decay across 75 groups. First group very large. |
| CollectionGroups | 4,600-18,400 records. PowerLaw fan-out of 2-8 active groups per collection. |
| Permissions | ~82% ReadOnly, ~9% ReadWrite, ~5% Manage, ~4% HidePasswords. |
| Orphan ciphers | ~2,550 of 17,000 (15% orphan rate). |
| Direct access ratio | 0.6 — ~60% of access paths are direct CollectionUser. |
| Empty group rate | 20% — ~15 of 75 groups have 0 members. |
| Collections per user | PowerLaw 1-30 (skew 0.7). First users get up to 30, most get 1-2. CV > 0.5. |
| Multi-collection rate | 30% of ~14,450 non-orphan ciphers in 2-4 collections. Max 4 per cipher. |

### 8. Weyland-Yutani (XL)

| Check | Expected |
|-------|----------|
| Membership shape | PowerLaw (skew 0.8) — very steep decay across 500 groups. Long tail of small groups. |
| CollectionGroups | 1,200-3,600 records. PowerLaw fan-out of 1-3 active groups per collection. |
| Permissions | ~55% ReadWrite, ~25% ReadOnly, ~10% Manage, ~10% HidePasswords. |
| Orphan ciphers | ~1,500 of 15,000 (10% orphan rate). |
| Direct access ratio | 0.4 — majority of access is group-mediated. |
| Empty group rate | ~68% — ~341 of 500 groups have 0 members due to power-law tail truncation. |
| Collections per user | PowerLaw 1-50 (skew 0.8). First users get up to 50, most get 1-2. CV > 0.5. |
| Multi-collection rate | 30% of ~13,500 non-orphan ciphers in 2-5 collections. Max 5 per cipher. |

### 9. Initech (XL)

| Check | Expected |
|-------|----------|
| Membership shape | MegaGroup (skew 0.95) — group 1 has ~93% of members, remaining 4 split the rest evenly. |
| CollectionGroups | 0 records. DirectAccessRatio is 1.0, so CollectionGroup creation is skipped entirely. |
| Permissions | ~30% Manage, ~30% ReadWrite, ~30% ReadOnly, ~10% HidePasswords. |
| Orphan ciphers | ~12,750 of 15,000 (85% orphan rate). |
| Direct access ratio | 1.0 — 100% of access paths are direct CollectionUser. |
| Collections per user | PowerLaw 1-20 (skew 0.5). First users get up to 20, most get 1. CV > 0.2. |
| Multi-collection rate | 15% of ~2,250 non-orphan ciphers in 2-3 collections. Max 3 per cipher. |

---

## Validation Preset Expected Values

### 1. Power-Law Distribution

```bash
dotnet run -- seed --preset validation.density-modeling-power-law-test --mangle
```

| Check | Expected |
|-------|----------|
| Groups | 10 groups. First has ~50 members, decays to 1. Last 2 have 0 members (20% empty rate). |
| CollectionGroups | > 0 records. First collections have more groups assigned (PowerLaw fan-out). |
| Permissions | ~50% ReadOnly, ~30% ReadWrite, ~15% Manage, ~5% HidePasswords. |
| Orphan ciphers | ~50 of 500 (10% orphan rate). |
| DirectAccessRatio | 0.6 — roughly 60% of access paths are direct CollectionUser. |

### 2. MegaGroup Distribution

```bash
dotnet run -- seed --preset validation.density-modeling-mega-group-test --mangle
```

| Check | Expected |
|-------|----------|
| Groups | 5 groups. Group 1 has ~90 members (90.5%). Groups 2-5 split ~10 members. |
| CollectionUsers | 0 records. DirectAccessRatio is 0.0 — all access via groups. |
| CollectionGroups | > 0. First 10 collections get 3 groups (FrontLoaded), rest get 1. |
| Permissions | 25% each for ReadOnly, ReadWrite, Manage, HidePasswords (even split). |

### 3. Empty Groups

```bash
dotnet run -- seed --preset validation.density-modeling-empty-groups-test --mangle
```

| Check | Expected |
|-------|----------|
| Groups | 10 groups total. 5 with ~10 members each, 5 with 0 members (50% empty). |
| CollectionGroups | Only reference the 5 non-empty groups. |
| DirectAccessRatio | 0.5 — roughly half of users get direct CollectionUser records. |

### 4. No Density (Baseline)

```bash
dotnet run -- seed --preset validation.density-modeling-no-density-test --mangle
```

| Check | Expected |
|-------|----------|
| Groups | 5 groups with ~10 members each (uniform round-robin). |
| CollectionGroups | 0 records. No density = no CollectionGroup generation. |
| Permissions | First assignment per user is Manage, subsequent are ReadOnly (original cycling pattern). |
| Orphan ciphers | 0. Every cipher assigned to at least one collection. |
