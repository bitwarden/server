# Density Modeling Validation Presets

These presets validate that the Seeder's density distribution algorithms produce correct relationship patterns. Run them, query the DB, and compare against the expected results below.

Always use the `--mangle` flag to avoid collisions with existing data.

## Verification Queries

Run the first query to get the Organization ID, then paste it into the remaining queries.

### Find the Organization ID

```sql
SELECT Id, [Name]
FROM [dbo].[Organization] WITH (NOLOCK)
WHERE [Name] = 'PASTE_ORG_NAME_HERE';
```

### Group Membership Distribution

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

### CollectionGroup Count

```sql
DECLARE @OrgId UNIQUEIDENTIFIER = 'PASTE_ORG_ID_HERE';

SELECT COUNT(*) AS CollectionGroupCount
FROM [dbo].[CollectionGroup] CG WITH (NOLOCK)
INNER JOIN [dbo].[Collection] C WITH (NOLOCK) ON CG.CollectionId = C.Id
WHERE C.OrganizationId = @OrgId;
```

### Permission Distribution

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

### Orphan Ciphers

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

---

## Presets

### 1. Power-Law Distribution

Tests skewed group membership, CollectionGroup generation, permission distribution, and cipher orphans.

```bash
cd util/SeederUtility
dotnet run -- seed --preset validation.density-modeling-power-law-test --mangle
```

| Check             | Expected                                                                               |
| ----------------- | -------------------------------------------------------------------------------------- |
| Groups            | 10 groups. First has ~50 members, decays to 1. Last 2 have 0 members (20% empty rate). |
| CollectionGroups  | > 0 records. First collections have more groups assigned (PowerLaw fan-out).           |
| Permissions       | ~50% ReadOnly, ~30% ReadWrite, ~15% Manage, ~5% HidePasswords.                         |
| Orphan ciphers    | ~50 of 500 (10% orphan rate).                                                          |
| DirectAccessRatio | 0.6 — roughly 60% of access paths are direct CollectionUser.                           |

### 2. MegaGroup Distribution

Tests one dominant group with all-group access (no direct CollectionUser).

```bash
cd util/SeederUtility
dotnet run -- seed --preset validation.density-modeling-mega-group-test --mangle
```

| Check            | Expected                                                                 |
| ---------------- | ------------------------------------------------------------------------ |
| Groups           | 5 groups. Group 1 has ~90 members (90.5%). Groups 2-5 split ~10 members. |
| CollectionUsers  | 0 records. DirectAccessRatio is 0.0 — all access via groups.             |
| CollectionGroups | > 0. First 10 collections get 3 groups (FrontLoaded), rest get 1.        |
| Permissions      | 25% each for ReadOnly, ReadWrite, Manage, HidePasswords (even split).    |

### 3. Empty Groups

Tests that EmptyGroupRate produces memberless groups excluded from CollectionGroup assignment.

```bash
cd util/SeederUtility
dotnet run -- seed --preset validation.density-modeling-empty-groups-test --mangle
```

| Check             | Expected                                                                           |
| ----------------- | ---------------------------------------------------------------------------------- |
| Groups            | 10 groups total. 5 with ~10 members each, 5 with 0 members (50% empty).            |
| CollectionGroups  | Only reference the 5 non-empty groups. Run `SELECT DISTINCT CG.GroupId` to verify. |
| DirectAccessRatio | 0.5 — roughly half of users get direct CollectionUser records.                     |

### 4. No Density (Baseline)

Confirms backward compatibility. No `density` block = original round-robin behavior.

```bash
cd util/SeederUtility
dotnet run -- seed --preset validation.density-modeling-no-density-test --mangle
```

| Check            | Expected                                                                                 |
| ---------------- | ---------------------------------------------------------------------------------------- |
| Groups           | 5 groups with ~10 members each (uniform round-robin).                                    |
| CollectionGroups | 0 records. No density = no CollectionGroup generation.                                   |
| Permissions      | First assignment per user is Manage, subsequent are ReadOnly (original cycling pattern). |
| Orphan ciphers   | 0. Every cipher assigned to at least one collection.                                     |
