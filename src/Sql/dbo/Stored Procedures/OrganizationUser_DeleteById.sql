CREATE PROCEDURE [dbo].[OrganizationUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
    
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT
        @OrganizationId = [OrganizationId],
        @UserId = [UserId]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL AND @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[SsoUser_Delete] @UserId, @OrganizationId
    END

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @Id

    EXEC [dbo].[OrganizationSponsorship_OrganizationUserDeleted] @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id
END
