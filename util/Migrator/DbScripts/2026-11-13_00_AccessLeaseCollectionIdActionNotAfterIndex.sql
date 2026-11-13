-- Replaces IX_AccessLease_CollectionId_Action with a covering index keyed on [NotAfter], so the
-- governance lease lists seek past naturally expired leases instead of reading every lease stored.
IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_CollectionId_Action_NotAfter'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Action_NotAfter]
    ON [dbo].[AccessLease] ([CollectionId] ASC, [Action] ASC, [NotAfter] ASC)
    INCLUDE ([AccessRequestId], [OrganizationId], [CipherId], [RequesterId], [NotBefore], [RevokedDate], [RevokedBy], [CreationDate]);
END
GO

IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_CollectionId_Action'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    DROP INDEX [IX_AccessLease_CollectionId_Action] ON [dbo].[AccessLease];
END
GO
