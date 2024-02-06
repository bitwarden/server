CREATE PROCEDURE [dbo].[Organization_RestorePreCollectionEnhancementsData]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

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
END
