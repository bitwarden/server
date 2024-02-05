CREATE PROCEDURE [dbo].[Organization_RestorePreCollectionEnhancementsData]
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
