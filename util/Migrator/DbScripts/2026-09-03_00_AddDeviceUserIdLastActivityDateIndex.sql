-- Add a nonclustered index on [Device].([UserId], [LastActivityDate], [Type]) for MemberAdoptionReport_ReadByOrganizationId.
IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_Device_UserId_LastActivityDate_Type'
        AND object_id = OBJECT_ID('[dbo].[Device]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Device_UserId_LastActivityDate_Type]
    ON [dbo].[Device] ([UserId] ASC, [LastActivityDate] ASC, [Type] ASC);
END
GO
