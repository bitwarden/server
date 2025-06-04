CREATE PROCEDURE [dbo].[Collection_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @ExternalId NVARCHAR(300),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @DefaultUserCollectionEmail NVARCHAR(256) = NULL,
    @Type TINYINT = 0
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Collection]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [ExternalId] = @ExternalId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [DefaultUserCollectionEmail] = @DefaultUserCollectionEmail,
        [Type] = @Type
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByCollectionId] @Id, @OrganizationId
END
