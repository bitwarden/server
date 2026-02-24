-- Consolidate DisableSend (type 6) policies into SendOptions (type 7) policies.
-- This is the Phase 1 (initial) migration: it populates SendOptions rows with
-- disableSend=true before the new code is deployed, without removing type 6 rows.
-- Type 6 rows are intentionally left in place so that a rollback to the previous
-- release continues to enforce the policy correctly.
-- The DELETE of type 6 rows is a breaking change deferred to DbScripts_finalization.

-- Step 1: For orgs that have DisableSend (type 6) enabled AND already have a
-- SendOptions (type 7) row, merge disableSend=true into the existing JSON data.
UPDATE [dbo].[Policy]
SET [Data] = JSON_MODIFY(ISNULL([Data], '{}'), '$.disableSend', CAST(1 AS BIT)),
    [RevisionDate] = GETUTCDATE()
WHERE [Type] = 7
  AND [OrganizationId] IN (
      SELECT [OrganizationId]
      FROM [dbo].[Policy]
      WHERE [Type] = 6
        AND [Enabled] = 1
  );
GO

-- Step 2: For orgs that have DisableSend (type 6) enabled but NO SendOptions (type 7)
-- row yet, insert a new enabled SendOptions row with disableSend=true.
INSERT INTO [dbo].[Policy] ([Id], [OrganizationId], [Type], [Data], [Enabled], [CreationDate], [RevisionDate])
SELECT
    NEWID(),
    ds.[OrganizationId],
    7,
    '{"disableSend":true}',
    1,
    GETUTCDATE(),
    GETUTCDATE()
FROM [dbo].[Policy] ds
WHERE ds.[Type] = 6
  AND ds.[Enabled] = 1
  AND NOT EXISTS (
      SELECT 1
      FROM [dbo].[Policy] so
      WHERE so.[OrganizationId] = ds.[OrganizationId]
        AND so.[Type] = 7
  );
GO
