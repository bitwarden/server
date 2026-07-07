-- AccessRule deletion is a soft-delete (DeletedDate is stamped and the row is kept for the audit trail). The unique
-- index on (OrganizationId, Name) predates soft-delete, so it still counts soft-deleted rows and blocks recreating a
-- rule with the name of a deleted one. Rebuild it as a filtered index over live rows only: a name is released the
-- moment its rule is deleted, while active rules stay unique per organization. Guarded on [has_filter] so re-runs are
-- no-ops (converting from an unfiltered to a filtered index can only ever remove rows, so it is always safe).
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AccessRule_OrganizationId_Name'
        AND [object_id] = OBJECT_ID('[dbo].[AccessRule]')
        AND [has_filter] = 0
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessRule_OrganizationId_Name]
        ON [dbo].[AccessRule] ([OrganizationId] ASC, [Name] ASC)
        WHERE [DeletedDate] IS NULL
        WITH (DROP_EXISTING = ON);
END
GO
