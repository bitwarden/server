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

IF NOT EXISTS (
    SELECT *  FROM sys.indexes  WHERE [Name]='IX_ProviderOrganization_OrganizationId'
    AND object_id = OBJECT_ID('[dbo].[ProviderOrganization]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_ProviderOrganization_OrganizationId]
        ON [dbo].[ProviderOrganization]([OrganizationId] ASC)
        WITH (ONLINE = ON)
END
GO

