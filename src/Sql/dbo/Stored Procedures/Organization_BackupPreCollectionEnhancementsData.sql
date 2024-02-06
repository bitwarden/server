CREATE PROCEDURE [dbo].[Organization_BackupPreCollectionEnhancementsData]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Backup Group Ids with `AccessAll = 1` for the specified OrganizationId
    INSERT INTO [dbo].[FCBackupAccessAllGroups] ([OrganizationId], [GroupId])
    SELECT @OrganizationId, [Id] AS [GroupId]
    FROM [dbo].[Group]
    WHERE [OrganizationId] = @OrganizationId
      AND [AccessAll] = 1;

    -- Backup OrganizationUser Ids with `AccessAll = 1` for the specified OrganizationId
    INSERT INTO [dbo].[FCBackupAccessAllOrganizationUsers] ([OrganizationId], [OrganizationUserId])
    SELECT @OrganizationId, [Id] AS [OrganizationUserId]
    FROM [dbo].[OrganizationUser]
    WHERE [OrganizationId] = @OrganizationId
      AND [AccessAll] = 1;

    -- Backup OrganizationUser Ids and Types before being migrated to User type for the specified OrganizationId
    INSERT INTO [dbo].[FCBackupOrganizationUserManagers] ([OrganizationId], [OrganizationUserId], [Type])
    SELECT @OrganizationId, [Id] AS [OrganizationUserId], [Type]
    FROM [dbo].[OrganizationUser]
    WHERE [OrganizationId] = @OrganizationId
        AND [Id] IN (
            SELECT [OrganizationUserId]
            FROM [dbo].[OrganizationUser]
            WHERE [OrganizationId] = @OrganizationId
                AND ([Type] = 3 OR
                    ([Type] = 4 AND
                    [Permissions] IS NOT NULL AND
                    ISJSON([Permissions]) > 0 AND
                    (JSON_VALUE([Permissions], '$.editAssignedCollections') = 'true' OR
                        JSON_VALUE([Permissions], '$.deleteAssignedCollections') = 'true'))
                )
        );
END
