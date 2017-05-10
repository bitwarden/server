CREATE PROCEDURE [dbo].[Group_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Group]
    (
        [Id],
        [OrganizationId],
        [Name],
        [AccessAll],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @AccessAll,
        @CreationDate,
        @RevisionDate
    )
END