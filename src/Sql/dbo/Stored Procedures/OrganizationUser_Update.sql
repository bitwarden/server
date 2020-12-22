CREATE PROCEDURE [dbo].[OrganizationUser_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Email NVARCHAR(50),
    @Key VARCHAR(MAX),
    @Status TINYINT,
    @Type TINYINT,
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @AccessBusinessPortal BIT,
    @AccessEventLogs BIT,
    @AccessImportExport BIT,
    @AccessReports BIT,
    @ManageAllCollections BIT,
    @ManageAssignedCollections BIT,
    @ManageGroups BIT,
    @ManagePolicies BIT,
    @ManageUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [OrganizationId] = @OrganizationId,
        [UserId] = @UserId,
        [Email] = @Email,
        [Key] = @Key,
        [Status] = @Status,
        [Type] = @Type,
        [AccessAll] = @AccessAll,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [AccessBusinessPortal] = @AccessBusinessPortal,
        [AccessEventLogs] = @AccessEventLogs,
        [AccessImportExport] = @AccessImportExport,
        [AccessReports] = @AccessReports,
        [ManageAllCollections] = @ManageAllCollections,
        [ManageAssignedCollections] = @ManageAssignedCollections,
        [ManageGroups] = @ManageGroups,
        [ManagePolicies] = @ManagePolicies,
        [ManageUsers] = @ManageUsers
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
