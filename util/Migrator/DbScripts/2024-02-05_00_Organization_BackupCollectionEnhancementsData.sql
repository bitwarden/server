-- Script to create backup pre-collection enhancements Organization data
    -- Table to store the groups with AccessAll = 1
    IF OBJECT_ID('[dbo].[FCBackupAccessAllGroups]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[FCBackupAccessAllGroups] (
            [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
            [GroupId]           UNIQUEIDENTIFIER    NOT NULL
            PRIMARY KEY CLUSTERED ([OrganizationId], [GroupId])
        );
    END
    GO

    -- Table to store the OrganizationUsers with AccessAll = 1
    IF OBJECT_ID('[dbo].[FCBackupAccessAllOrganizationUsers]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[FCBackupAccessAllOrganizationUsers] (
            [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
            [OrganizationUserId]    UNIQUEIDENTIFIER    NOT NULL,
            PRIMARY KEY CLUSTERED ([OrganizationId], [OrganizationUserId])
        );
    END
    GO

    -- Table to store the OrganizationUsers that were previously Manager
    -- or had Edit/Delete AssignedCollections permissions
    IF OBJECT_ID('[dbo].[FCBackupOrganizationUserManagers]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[FCBackupOrganizationUserManagers] (
            [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
            [OrganizationUserId]    UNIQUEIDENTIFIER    NOT NULL,
            [Type]                  INT                 NOT NULL,
            PRIMARY KEY CLUSTERED ([OrganizationId], [OrganizationUserId])
        );
    END
    GO

-- Stored procedure to backup pre-collection enhancements Organization data
CREATE OR ALTER PROCEDURE [dbo].[Organization_BackupPreCollectionEnhancementsData]
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
        AND ([Type] = 3
            OR ([Type] = 4
                AND [Permissions] IS NOT NULL
                AND ISJSON([Permissions]) > 0
                AND (JSON_VALUE([Permissions], '$.editAssignedCollections') = 'true'
                    OR JSON_VALUE([Permissions], '$.deleteAssignedCollections') = 'true')
            )
        );
END
GO

-- Stored procedure to restore pre-collection enhancements Organization data
CREATE OR ALTER PROCEDURE [dbo].[Organization_RestorePreCollectionEnhancementsData]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;
            -- Restore Group data with `AccessAll = 1`
            UPDATE G
            SET G.[AccessAll] = 1, G.[RevisionDate] = GETUTCDATE()
            FROM [dbo].[Group] G
            INNER JOIN [dbo].[FCBackupAccessAllGroups] BG ON G.[Id] = BG.[GroupId]
            WHERE G.[OrganizationId] = @OrganizationId;

            -- Restore OrganizationUser data with `AccessAll = 1`
            UPDATE OU
            SET OU.[AccessAll] = 1, OU.[RevisionDate] = GETUTCDATE()
            FROM [dbo].[OrganizationUser] OU
            INNER JOIN [dbo].[FCBackupAccessAllOrganizationUsers] BOU ON OU.[Id] = BOU.[OrganizationUserId]
            WHERE OU.[OrganizationId] = @OrganizationId;

            -- Restore OrganizationUser Types that were Manager/Custom with Edit/Delete Assigned Collections permissions
            UPDATE OU
            SET OU.[Type] = BOU.[Type], OU.[RevisionDate] = GETUTCDATE()
            FROM [dbo].[OrganizationUser] OU
            INNER JOIN [dbo].[FCBackupOrganizationUserManagers] BOU ON OU.[Id] = BOU.[OrganizationUserId]
            WHERE OU.[OrganizationId] = @OrganizationId;

            -- Disable FlexibleCollections for the Organization
            UPDATE [dbo].[Organization]
            SET [FlexibleCollections] = 0, [RevisionDate] = GETUTCDATE()
            WHERE [Id] = @OrganizationId;

            -- Delete backup data for the specified OrganizationId
            DELETE FROM [dbo].[FCBackupAccessAllGroups] WHERE [OrganizationId] = @OrganizationId;
            DELETE FROM [dbo].[FCBackupAccessAllOrganizationUsers] WHERE [OrganizationId] = @OrganizationId;
            DELETE FROM [dbo].[FCBackupOrganizationUserManagers] WHERE [OrganizationId] = @OrganizationId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
GO
