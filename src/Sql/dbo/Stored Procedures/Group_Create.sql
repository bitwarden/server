CREATE PROCEDURE [dbo].[Group_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(100),
    @AccessAll BIT,
    @ExternalId NVARCHAR(300),
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
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @AccessAll,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )
END
