-- Add a filtered nonclustered index on [Event].[OrganizationId].
-- Event_DeleteManyByOrganizationId deletes events in TOP(1000) batches filtered by
-- [OrganizationId]; without this index each batch performs a table scan. The filter
-- (OrganizationId IS NOT NULL) keeps the index small since many events are user-scoped.
IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_Event_OrganizationId'
        AND object_id = OBJECT_ID('[dbo].[Event]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Event_OrganizationId]
    ON [dbo].[Event] ([OrganizationId] ASC)
    WHERE [OrganizationId] IS NOT NULL;
END
GO
