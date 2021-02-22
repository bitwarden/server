IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_OrganizationUser_StatusType'
    AND object_id = OBJECT_ID('[dbo].[OrganizationUser]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationUser_StatusType]
        ON [dbo].[OrganizationUser] ([Status], [Type])
        INCLUDE ([OrganizationId], [UserId]);
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_EmergencyAccess_GrantorId'
    AND object_id = OBJECT_ID('[dbo].[EmergencyAccess]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_EmergencyAccess_GrantorId]
        ON [dbo].[EmergencyAccess] ([GrantorId]);
END
GO

IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_Group_OrganizationId'
    AND object_id = OBJECT_ID('[dbo].[OrganizationId]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Group_OrganizationId]
        ON [dbo].[Group] ([OrganizationId]);
END
GO


IF NOT EXISTS (
    SELECT * FROM sys.indexes  WHERE [Name]='IX_SsoConfig_OrganizationId'
    AND object_id = OBJECT_ID('[dbo].[SsoConfig]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SsoConfig_OrganizationId]
        ON [dbo].[SsoConfig] ([OrganizationId])
        INCLUDE ([CreationDate], [Data], [Enabled], [RevisionDate]);
END
GO