-- Filtered index supporting Event_ReadPageBySendId (OrganizationId + SendId + Date window).
-- Send rows are a tiny minority of [Event], so filtering on SendId keeps the index small.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_Event_OrganizationIdSendIdDate'
        AND [object_id] = OBJECT_ID('[dbo].[Event]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Event_OrganizationIdSendIdDate]
        ON [dbo].[Event] ([OrganizationId] ASC, [SendId] ASC, [Date] DESC)
        WHERE [SendId] IS NOT NULL;
END
GO
