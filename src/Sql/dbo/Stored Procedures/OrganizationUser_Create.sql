CREATE PROCEDURE [dbo].[OrganizationUser_Create]
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

    INSERT INTO [dbo].[OrganizationUser]
    (
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [AccessBusinessPortal],
        [AccessEventLogs],
        [AccessImportExport],
        [AccessReports],
        [ManageAllCollections],
        [ManageAssignedCollections],
        [ManageGroups],
        [ManagePolicies],
        [ManageUsers]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @UserId,
        @Email,
        @Key,
        @Status,
        @Type,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate,
        @AccessBusinessPortal,
        @AccessEventLogs,
        @AccessImportExport,
        @AccessReports,
        @ManageAllCollections,
        @ManageAssignedCollections,
        @ManageGroups,
        @ManagePolicies,
        @ManageUsers
    )
END
