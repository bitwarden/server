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

    -- Backup Manager Ids before being migrated to User type for the specified OrganizationId
    INSERT INTO [dbo].[FCBackupOrganizationUserManagers] ([OrganizationId], [OrganizationUserId])
    SELECT @OrganizationId, [Id] AS [OrganizationUserId]
    FROM [dbo].[OrganizationUser]
    WHERE [OrganizationId] = @OrganizationId
        AND [Type] = 3;

    INSERT INTO [dbo].[FCBackupCollectionUserColumns] ([OrganizationId], [CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords])
    SELECT @OrganizationId, CU.[CollectionId], CU.[OrganizationUserId], CU.[ReadOnly], CU.[HidePasswords]
    FROM [dbo].[CollectionUser] CU
    INNER JOIN [dbo].[Collection] C ON CU.[CollectionId] = C.[Id]
    INNER JOIN [dbo].[OrganizationUser] OU ON CU.[OrganizationUserId] = OU.[Id]
    WHERE C.[OrganizationId] = @OrganizationId
        AND (OU.[Type] = 3
            OR (OU.[Type] = 4
                AND OU.[Permissions] IS NOT NULL
                AND ISJSON(OU.[Permissions]) > 0
                AND (JSON_VALUE(OU.[Permissions], '$.editAssignedCollections') = 'true')
            )
        );
END
