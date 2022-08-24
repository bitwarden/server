IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name]='IX_SsoConfig_OrganizationId'
    AND object_id = OBJECT_ID('[dbo].[SsoConfig]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SsoConfig_OrganizationId]
        ON [dbo].[SsoConfig]([OrganizationId] ASC)
        WITH (ONLINE = ON)
END
GO