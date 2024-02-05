-- Script to create backup pre-collection enhancements Organization data
    -- Table to store the groups with AccessAll = 1
    CREATE TABLE [dbo].[FCBackupAccessAllGroups] (
        [GroupId] UNIQUEIDENTIFIER PRIMARY KEY CLUSTERED
    );

    -- Table to store the OrganizationUsers with AccessAll = 1
    CREATE TABLE [dbo].[FCBackupAccessAllOrganizationUsers] (
        [OrganizationUserId] UNIQUEIDENTIFIER PRIMARY KEY CLUSTERED,
        [Type] INT
    );

    -- Table to store the OrganizationUsers that were previously Manager
    -- or had Edit/Delete AssignedCollections permissions
    CREATE TABLE [dbo].[FCBackupOrgUserManagers] (
        [OrganizationUserId] UNIQUEIDENTIFIER PRIMARY KEY CLUSTERED,
        [Type] INT
    );

-- Stored procedure to backup pre-collection enhancements Organization data
CREATE OR ALTER PROCEDURE [dbo].[Organization_BackupPreCollectionEnhancementsData]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Backup Group Ids with AccessAll = 1
    SELECT [Id] AS [GroupId]
    INTO [dbo].[FCBackupAccessAllGroups]
    FROM [dbo].[Group]
    WHERE [OrganizationId] = @OrganizationId
      AND [AccessAll] = 1;

    -- Backup OrganizationUser Ids with AccessAll = 1
    SELECT [Id] AS [OrganizationUserId], [Type]
    INTO [dbo].[FCBackupAccessAllOrganizationUsers]
    FROM [dbo].[OrganizationUser]
    WHERE [OrganizationId] = @OrganizationId
      AND [AccessAll] = 1;

    -- Backup OrganizationUser Ids and Types before being migrated to User type
    SELECT [Id] AS [OrganizationUserId], [Type]
    INTO [dbo].[FCBackupOrgUserManagers]
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
GO

-- Stored procedure to restore pre-collection enhancements Organization data
CREATE OR ALTER PROCEDURE [dbo].[Organization_RestorePreCollectionEnhancementsData]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Restore Group data with AccessAll = 1
    UPDATE G
    SET G.[AccessAll] = 1
    FROM [dbo].[Group] G
    INNER JOIN [dbo].[FCBackupAccessAllGroups] BG ON G.[Id] = BG.[GroupId]
    WHERE G.[OrganizationId] = @OrganizationId;

    -- Restore OrganizationUser data with AccessAll = 1
    UPDATE OU
    SET OU.[AccessAll] = 1,
        OU.[Type] = BOU.[Type]
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN [dbo].[FCBackupAccessAllOrganizationUsers] BOU ON OU.[Id] = BOU.[OrganizationUserId]
    WHERE OU.[OrganizationId] = @OrganizationId;

    -- Restore OrganizationUser Types that were Manager/Custom with Edit/Delete Assigned Collections permissions
    UPDATE OU
    SET OU.[Type] = BOU.[Type]
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN [dbo].[FCBackupOrgUserManagers] BOU ON OU.[Id] = BOU.[OrganizationUserId]
    WHERE OU.[OrganizationId] = @OrganizationId;
END
GO
