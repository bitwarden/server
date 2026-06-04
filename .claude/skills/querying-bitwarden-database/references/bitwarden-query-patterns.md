# Bitwarden Query Patterns

Concrete SQL recipes. MSSQL syntax; patterns translate to other providers by substituting `JSON_VALUE`, `[Brackets]`, `UNIQUEIDENTIFIER`.

## Cipher access: use the UserCipherDetails TVF

The canonical TVF ([sources.md](sources.md#user-cipher-details-fn)) returns every cipher the user can see with `Edit` / `ViewPassword` / `Manage` resolved.

```sql
SELECT * FROM [dbo].[UserCipherDetails](@UserId)
WHERE [DeletedDate] IS NULL;
```

The TVF unions four kinds of rows; reimplementations must replicate all four:

1. **Personal vault** — `Cipher.UserId = @UserId`. Permissions hard-coded `Edit=1, ViewPassword=1, Manage=1`.
2. **Org direct** — `OrganizationUser (Status=2) → CollectionUser → CollectionCipher → Cipher`.
3. **Org group-mediated** — `OrganizationUser → GroupUser → CollectionGroup → CollectionCipher → Cipher`. Followed only when no direct `CollectionUser` row exists for that user/collection — prevents double-counting.
4. **Org gate** — `Organization.Enabled = 1` required. Disabled-org ciphers silently hidden.

## Permission resolution

```sql
Edit         = CASE WHEN COALESCE(CU.[ReadOnly],      CG.[ReadOnly],      0) = 0 THEN 1 ELSE 0 END
ViewPassword = CASE WHEN COALESCE(CU.[HidePasswords], CG.[HidePasswords], 0) = 0 THEN 1 ELSE 0 END
Manage       = CASE WHEN COALESCE(CU.[Manage],        CG.[Manage],        0) = 1 THEN 1 ELSE 0 END
```

Direct user grants (`CU`) beat group grants (`CG`). `Manage` is positive; `ReadOnly`/`HidePasswords` are negative, hence the flip to derived `Edit` / `ViewPassword`.

[sources.md](sources.md#user-cipher-details-fn)

## Manual enumeration — when the TVF doesn't fit

For aggregation inside access enumeration (e.g., count org ciphers a user can manage, grouped by collection):

```sql
WITH ConfirmedMemberships AS (
    SELECT [Id] AS [OrganizationUserId], [OrganizationId]
    FROM [dbo].[OrganizationUser]
    WHERE [UserId] = @UserId AND [Status] = 2
),
DirectAccess AS (
    SELECT CC.[CipherId], CU.[CollectionId], CU.[ReadOnly], CU.[HidePasswords], CU.[Manage], 'Direct' AS [AccessType]
    FROM [dbo].[CollectionUser] CU
    JOIN ConfirmedMemberships M ON CU.[OrganizationUserId] = M.[OrganizationUserId]
    JOIN [dbo].[CollectionCipher] CC ON CC.[CollectionId] = CU.[CollectionId]
),
GroupAccess AS (
    SELECT CC.[CipherId], CG.[CollectionId], CG.[ReadOnly], CG.[HidePasswords], CG.[Manage], 'Group' AS [AccessType]
    FROM [dbo].[GroupUser] GU
    JOIN ConfirmedMemberships M ON GU.[OrganizationUserId] = M.[OrganizationUserId]
    JOIN [dbo].[CollectionGroup] CG ON CG.[GroupId] = GU.[GroupId]
    JOIN [dbo].[CollectionCipher] CC ON CC.[CollectionId] = CG.[CollectionId]
),
AllAccess AS (
    SELECT * FROM DirectAccess UNION ALL SELECT * FROM GroupAccess
)
SELECT
    C.[Id],
    A.[CollectionId],
    A.[AccessType],
    CASE WHEN MIN(CAST(A.[ReadOnly]      AS INT)) = 0 THEN 1 ELSE 0 END AS [Edit],
    CASE WHEN MIN(CAST(A.[HidePasswords] AS INT)) = 0 THEN 1 ELSE 0 END AS [ViewPassword],
    CASE WHEN MAX(CAST(A.[Manage]        AS INT)) = 1 THEN 1 ELSE 0 END AS [Manage]
FROM AllAccess A
JOIN [dbo].[Cipher] C ON C.[Id] = A.[CipherId]
JOIN [dbo].[Organization] O ON O.[Id] = C.[OrganizationId] AND O.[Enabled] = 1
WHERE C.[DeletedDate] IS NULL
GROUP BY C.[Id], A.[CollectionId], A.[AccessType];
```

This flattens permissions with `MIN/MAX` ("least restrictive wins"), which differs from the TVF's `COALESCE(CU, CG, 0)` direct-beats-group rule. For TVF-exact semantics, prefer the TVF.

## Collection access (direct + group)

Two independent paths — querying only one silently understates access. Full permission resolution above.

```sql
-- Direct access via CollectionUser
SELECT C.[Id], C.[Name], CU.[ReadOnly], CU.[HidePasswords], CU.[Manage], 'Direct' AS [AccessType]
FROM [dbo].[CollectionUser] CU
JOIN [dbo].[Collection] C        ON CU.[CollectionId]       = C.[Id]
JOIN [dbo].[OrganizationUser] OU ON CU.[OrganizationUserId] = OU.[Id]
WHERE OU.[UserId] = @UserId AND OU.[OrganizationId] = @OrgId AND OU.[Status] = 2
UNION ALL
-- Group access via CollectionGroup
SELECT C.[Id], C.[Name], CG.[ReadOnly], CG.[HidePasswords], CG.[Manage], 'Group' AS [AccessType]
FROM [dbo].[CollectionGroup] CG
JOIN [dbo].[Collection] C        ON CG.[CollectionId]       = C.[Id]
JOIN [dbo].[GroupUser] GU        ON CG.[GroupId]            = GU.[GroupId]
JOIN [dbo].[OrganizationUser] OU ON GU.[OrganizationUserId] = OU.[Id]
WHERE OU.[UserId] = @UserId AND OU.[OrganizationId] = @OrgId AND OU.[Status] = 2;
```

A user can hold both rows; merge in the caller. See grounding rule 6.

## Send: three dates, one disabled bit, plus access-count cap

```sql
SELECT [Id], [Type], [Data], [Key],
    [DeletionDate], [ExpirationDate], [Disabled], [AccessCount], [MaxAccessCount]
FROM [dbo].[Send]
WHERE [UserId] = @UserId
  AND [DeletionDate] > GETUTCDATE()
  AND ([ExpirationDate] IS NULL OR [ExpirationDate] > GETUTCDATE())
  AND [Disabled] = 0
  AND ([MaxAccessCount] IS NULL OR [AccessCount] < [MaxAccessCount]);
```

[sources.md](sources.md#send-table) — `Send_ReadByDeletionDateBefore` filters on `DeletionDate`, not `ExpirationDate`.

## AuthRequest: pending = NULL ResponseDate, expiry is a runtime offset

No `ExpirationDate` column — expiry computed from `CreationDate`/`ResponseDate` offsets. See `src/Sql/dbo/Auth/Stored Procedures/AuthRequest_DeleteIfExpired.sql`.

```sql
-- Currently pending login requests for a user
SELECT [Id], [Type], [CreationDate], [RequestDeviceType]
FROM [dbo].[AuthRequest]
WHERE [UserId]       = @UserId
  AND [ResponseDate] IS NULL                                  -- pending
  AND [CreationDate] > DATEADD(SECOND, -900, GETUTCDATE());  -- < 15 minutes old
```

## Organization feature flags — prefer `OrganizationAbilityView`

```sql
SELECT * FROM [dbo].[OrganizationAbilityView] WHERE [Id] = @OrgId;
```

[sources.md](sources.md#organization-ability-view)

## Common "active" filters

Three filters compose for almost every "what's live" question:

```sql
WHERE OU.[Status] = 2          -- Confirmed members
  AND O.[Enabled] = 1          -- Org is runtime-enabled
  AND C.[DeletedDate] IS NULL  -- Cipher is not in trash
```
