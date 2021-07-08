CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Collection]
    (
        [Id],
        [OrganizationId],
        [Name],
        [ExternalId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ExternalId,
        @CreationDate,
        @RevisionDate
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
