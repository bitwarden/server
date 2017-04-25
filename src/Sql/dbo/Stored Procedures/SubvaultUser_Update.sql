CREATE PROCEDURE [dbo].[SubvaultUser_Update]
    @Id UNIQUEIDENTIFIER,
    @SubvaultId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SubvaultUser]
    SET
        [SubvaultId] = @SubvaultId,
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