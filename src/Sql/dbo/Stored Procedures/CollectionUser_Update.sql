CREATE PROCEDURE [dbo].[CollectionUser_Update]
    @Id UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[CollectionUser]
    SET
        [CollectionId] = @CollectionId,
        [OrganizationUserId] = @OrganizationUserId,
        [ReadOnly] = @ReadOnly,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id

    IF @OrganizationUserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
    END
END