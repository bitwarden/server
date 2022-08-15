CREATE PROCEDURE [dbo].[Project_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7),
    @DeletedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE 
        [dbo].[Project]
    SET 
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DeletedDate] = @DeletedDate
    WHERE 
        [Id] = @Id
END