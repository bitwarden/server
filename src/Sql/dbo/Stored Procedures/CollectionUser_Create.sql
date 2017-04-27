CREATE PROCEDURE [dbo].[CollectionUser_Create]
    @Id UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[CollectionUser]
    (
        [Id],
        [CollectionId],
        [OrganizationUserId],
        [ReadOnly],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @CollectionId,
        @OrganizationUserId,
        @ReadOnly,
        @CreationDate,
        @RevisionDate
    )

    IF @OrganizationUserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
    END
END