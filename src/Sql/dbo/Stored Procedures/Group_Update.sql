CREATE PROCEDURE [dbo].[Group_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @AccessAll BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Group]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [AccessAll] = @AccessAll,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END