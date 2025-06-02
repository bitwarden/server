CREATE PROCEDURE [dbo].[Collection_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @UserDefaultCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
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
        [RevisionDate],
        [UserDefaultCollectionEmail],
        [Type]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @ExternalId,
        @CreationDate,
        @RevisionDate,
        @UserDefaultCollectionEmail,
     @Type
    )

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
