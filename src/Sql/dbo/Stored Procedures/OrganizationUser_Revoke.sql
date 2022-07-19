CREATE PROCEDURE [dbo].[OrganizationUser_Revoke]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = -1 -- Revoked
    WHERE
        [Id] = @Id

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
