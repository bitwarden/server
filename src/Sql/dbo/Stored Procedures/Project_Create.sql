CREATE PROCEDURE [dbo].[Project_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7),
    @DeletedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Project]
    (
        Id,
        OrganizationId, 
        Name,
        CreationDate,
        RevisionDate,
        DeletedDate
    )
    VALUES 
    (
        @Id,
        @OrganizationId,
        @Name,
        @CreationDate,
        @RevisionDate,
        @DeletedDate
    )
END