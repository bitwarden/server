CREATE PROCEDURE [dbo].[Organization_RestorePreCollectionEnhancementsData]
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

            -- Restore [dbo].[CollectionUser] columns [ReadOnly] and [HidePasswords]
            UPDATE CU
            SET CU.[ReadOnly] = BCU.[ReadOnly], CU.[HidePasswords] = BCU.[HidePasswords], CU.[Manage] = 0
            FROM [dbo].[CollectionUser] CU
            INNER JOIN [dbo].[FCBackupCollectionUserColumns] BCU
                ON CU.[CollectionId] = BCU.[CollectionId] AND CU.[OrganizationUserId] = BCU.[OrganizationUserId]
            INNER JOIN [dbo].[Collection] C ON CU.[CollectionId] = C.[Id]
            WHERE C.[OrganizationId] = @OrganizationId;

            -- Disable FlexibleCollections for the Organization
            UPDATE [dbo].[Organization]
            SET [FlexibleCollections] = 0, [RevisionDate] = GETUTCDATE()
            WHERE [Id] = @OrganizationId;

            -- Delete backup data for the specified OrganizationId
            DELETE FROM [dbo].[FCBackupAccessAllGroups] WHERE [OrganizationId] = @OrganizationId;
            DELETE FROM [dbo].[FCBackupAccessAllOrganizationUsers] WHERE [OrganizationId] = @OrganizationId;
            DELETE FROM [dbo].[FCBackupOrganizationUserManagers] WHERE [OrganizationId] = @OrganizationId;
            DELETE FROM [dbo].[FCBackupCollectionUserColumns] WHERE [OrganizationId] = @OrganizationId;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END
